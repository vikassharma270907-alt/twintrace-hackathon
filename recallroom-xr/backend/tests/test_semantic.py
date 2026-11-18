"""
Unit tests for the Semantic Evaluation Service in RecallRoom XR.
Validates the calibrated 3-tier policy:
  - CORRECT (>= 0.75)
  - PARTIAL (0.50 - 0.74)
  - RETRY   (< 0.50)
"""

import pytest
from app.schemas.requests import EvaluationRequest
from app.services.semantic_service import semantic_service


class TestSemanticEvaluation:
    """Test suite covering semantic evaluation algorithms and 3-tier calibration."""

    def test_semantic_correct_tier(self):
        """High-fidelity recall should score >= 0.75 and be marked CORRECT."""
        req = EvaluationRequest(
            concept_id="net_tcp_01",
            concept_title="TCP Three-Way Handshake",
            expected_answer="The TCP three-way handshake establishes a reliable connection between client and server using SYN, SYN-ACK, and ACK packets.",
            student_transcript="The TCP three-way handshake sets up a connection between a client and a server using SYN, SYN-ACK, and ACK packets.",
            keywords=["SYN", "SYN-ACK", "ACK", "client", "server"]
        )
        res = semantic_service.evaluate(req)

        assert res.similarity_score >= 0.75
        assert res.status == "CORRECT"
        assert "SYN" in res.matched_keywords
        assert "ACK" in res.matched_keywords
        assert len(res.missed_keywords) == 0
        assert "Outstanding recall" in res.feedback

    def test_semantic_partial_tier(self):
        """Partial recall missing critical depth should score in [0.50, 0.74] and be marked PARTIAL."""
        req = EvaluationRequest(
            concept_id="net_tcp_01",
            concept_title="TCP Three-Way Handshake",
            expected_answer="The TCP three-way handshake establishes a reliable connection between client and server using SYN, SYN-ACK, and ACK packets.",
            student_transcript="TCP uses SYN packets and ACK to connect computers.",
            keywords=["SYN", "SYN-ACK", "ACK", "client", "server"]
        )
        res = semantic_service.evaluate(req)

        assert 0.50 <= res.similarity_score < 0.75
        assert res.status == "PARTIAL"
        assert "SYN-ACK" in res.missed_keywords
        assert "Good progress" in res.feedback

    def test_semantic_retry_tier(self):
        """Irrelevant or completely incorrect response should score < 0.50 and be marked RETRY."""
        req = EvaluationRequest(
            concept_id="net_tcp_01",
            concept_title="TCP Three-Way Handshake",
            expected_answer="The TCP three-way handshake establishes a reliable connection between client and server using SYN, SYN-ACK, and ACK packets.",
            student_transcript="Mitochondria are the powerhouse of the biological cell generating ATP.",
            keywords=["SYN", "SYN-ACK", "ACK", "client", "server"]
        )
        res = semantic_service.evaluate(req)

        assert res.similarity_score < 0.50
        assert res.status == "RETRY"
        assert len(res.matched_keywords) == 0
        assert len(res.missed_keywords) == 5
        assert "Needs review" in res.feedback

    def test_empty_transcript_handling(self):
        """Empty student speech should immediately trigger RETRY with 0.0 similarity."""
        req = EvaluationRequest(
            concept_id="empty_test",
            concept_title="Empty Recall Test",
            expected_answer="Standard expected concept definition.",
            student_transcript="   ",
            keywords=["standard", "concept"]
        )
        res = semantic_service.evaluate(req)

        assert res.similarity_score == 0.0
        assert res.status == "RETRY"
        assert "No verbal recall detected" in res.feedback

    def test_accepted_answers_variation(self):
        """Matches against accepted_answers when student uses alternative valid formulation."""
        req = EvaluationRequest(
            concept_id="solar_heliocentric_02",
            concept_title="Heliocentrism",
            expected_answer="The astronomical model in which the Earth and planets revolve around the Sun at the center of the solar system.",
            student_transcript="The Copernican model states planets orbit around the Sun rather than Earth.",
            accepted_answers=[
                "The Copernican model states planets orbit around the Sun rather than Earth.",
                "Planetary theory placing the Sun at the center with orbital paths."
            ],
            keywords=["Sun", "planets", "orbit"]
        )
        res = semantic_service.evaluate(req)

        assert res.similarity_score >= 0.75
        assert res.status == "CORRECT"

    def test_custom_threshold_overrides(self):
        """Custom strict thresholds should govern the tier assignment."""
        req = EvaluationRequest(
            concept_id="strict_test",
            concept_title="Strict Exam Concept",
            expected_answer="Supervised learning trains models on labeled input-output pairs.",
            student_transcript="Supervised learning trains on labeled data pairs.",
            threshold_correct=0.95,  # Stricter than default 0.75
            threshold_partial=0.70
        )
        res = semantic_service.evaluate(req)

        # Even with high base similarity (~0.8), strict threshold moves it to PARTIAL
        assert res.similarity_score < 0.95
        assert res.status == "PARTIAL"

    def test_casing_and_punctuation_robustness(self):
        """Case variation and punctuation should not degrade semantic similarity."""
        base_req = EvaluationRequest(
            concept_id="case_test",
            concept_title="Case Test",
            expected_answer="Deep Neural Networks utilize backpropagation.",
            student_transcript="deep neural networks utilize backpropagation!!!",
            keywords=["deep", "neural", "networks"]
        )
        res = semantic_service.evaluate(base_req)
        assert res.status == "CORRECT"
        assert res.similarity_score >= 0.75
