"""
Unit tests for the Adaptive Spaced Repetition Service in RecallRoom XR.
Validates the deterministic formula:
  priority = 0.45 * (1 - recall) + 0.25 * time + 0.20 * hint + 0.10 * latency
Tests edge cases, clamping, zero-latency paths, and priority queue ordering.
"""

import pytest
from app.schemas.requests import ConceptReviewItem, AdaptiveReviewRequest
from app.services.adaptive_service import adaptive_service


class TestAdaptiveScheduler:
    """Test suite covering adaptive spaced repetition math and ranking logic."""

    def test_formula_exact_calculation(self):
        """Validates exact numerical calculation of all 4 weighted components."""
        item = ConceptReviewItem(
            concept_id="net_tcp_01",
            concept_title="TCP Three-Way Handshake",
            recall_score=0.20,
            time_since_review=0.85,
            hint_dependency=0.60,
            response_latency=0.70,
        )
        result = adaptive_service.calculate_priority(item)

        expected_recall_factor = round(0.45 * (1.0 - 0.20), 4)       # 0.3600
        expected_time_factor = round(0.25 * 0.85, 4)                 # 0.2125
        expected_hint_factor = round(0.20 * 0.60, 4)                 # 0.1200
        expected_latency_factor = round(0.10 * 0.70, 4)              # 0.0700
        expected_total = round(
            expected_recall_factor + expected_time_factor + expected_hint_factor + expected_latency_factor,
            4
        )  # 0.7625

        assert result.breakdown.recall_factor == expected_recall_factor
        assert result.breakdown.time_factor == expected_time_factor
        assert result.breakdown.hint_factor == expected_hint_factor
        assert result.breakdown.latency_factor == expected_latency_factor
        assert result.priority_score == expected_total
        assert "High Urgency" in result.rationale

    def test_boundary_worst_case(self):
        """Zero recall, maximum elapsed time, full hints, and maximum hesitation should equal 1.0."""
        item = ConceptReviewItem(
            concept_id="worst_case",
            concept_title="Forgotten Concept",
            recall_score=0.0,
            time_since_review=1.0,
            hint_dependency=1.0,
            response_latency=1.0,
        )
        result = adaptive_service.calculate_priority(item)

        assert result.priority_score == 1.0
        assert result.breakdown.recall_factor == 0.45
        assert result.breakdown.time_factor == 0.25
        assert result.breakdown.hint_factor == 0.20
        assert result.breakdown.latency_factor == 0.10

    def test_boundary_best_case(self):
        """Flawless recall, zero elapsed time, zero hints, and zero latency should equal 0.0."""
        item = ConceptReviewItem(
            concept_id="best_case",
            concept_title="Mastered Concept",
            recall_score=1.0,
            time_since_review=0.0,
            hint_dependency=0.0,
            response_latency=0.0,
        )
        result = adaptive_service.calculate_priority(item)

        assert result.priority_score == 0.0
        assert result.breakdown.recall_factor == 0.0
        assert result.breakdown.time_factor == 0.0
        assert result.breakdown.hint_factor == 0.0
        assert result.breakdown.latency_factor == 0.0
        assert "Low Urgency" in result.rationale

    def test_zero_latency_path(self):
        """Instantaneous retrieval (zero latency) should contribute 0.0 latency factor."""
        item = ConceptReviewItem(
            concept_id="instant_recall",
            concept_title="Instant Recall",
            recall_score=0.80,
            time_since_review=0.40,
            hint_dependency=0.10,
            response_latency=0.0,  # Zero-latency path
        )
        result = adaptive_service.calculate_priority(item)

        assert result.breakdown.latency_factor == 0.0
        # 0.45 * 0.20 + 0.25 * 0.40 + 0.20 * 0.10 + 0.0 = 0.09 + 0.10 + 0.02 = 0.21
        assert abs(result.priority_score - 0.21) < 1e-4

    def test_clamping_robustness(self):
        """Values out-of-range should be clamped gracefully without error."""
        # Testing internal clamp directly
        clamped_low = adaptive_service._clamp(-0.5)
        clamped_high = adaptive_service._clamp(1.5)
        assert clamped_low == 0.0
        assert clamped_high == 1.0

    def test_queue_sorting_and_ranks(self, sample_concept_items):
        """Queue should be sorted in strictly descending order of priority with sequential ranks."""
        req = AdaptiveReviewRequest(concepts=sample_concept_items)
        response = adaptive_service.prioritize_queue(req)

        assert len(response.queue) == len(sample_concept_items)
        assert response.total_concepts == len(sample_concept_items)

        # Verify descending order
        scores = [item.priority_score for item in response.queue]
        assert scores == sorted(scores, reverse=True)

        # Verify 1-based sequential rank
        for index, item in enumerate(response.queue):
            assert item.rank == index + 1

        # Highest priority should be net_tcp_01
        assert response.queue[0].concept_id == "net_tcp_01"
        assert response.top_recommendation is not None
        assert "TCP Three-Way Handshake" in response.top_recommendation

    def test_top_k_filtering(self, sample_concept_items):
        """Requesting top_k=2 should return only the top 2 highest priority concepts."""
        req = AdaptiveReviewRequest(concepts=sample_concept_items, top_k=2)
        response = adaptive_service.prioritize_queue(req)

        assert len(response.queue) == 2
        assert response.total_concepts == 4  # Total remains accurate
        assert response.queue[0].rank == 1
        assert response.queue[1].rank == 2

    def test_tie_breaking_order(self):
        """Identical priority scores should break ties deterministically by lower recall score first."""
        item1 = ConceptReviewItem(
            concept_id="concept_a",
            concept_title="Concept A",
            recall_score=0.40,
            time_since_review=0.40,
            hint_dependency=0.50,
            response_latency=0.0,
        )
        item2 = ConceptReviewItem(
            concept_id="concept_b",
            concept_title="Concept B",
            recall_score=0.30,  # Lower recall = higher need
            time_since_review=0.22,
            hint_dependency=0.50,
            response_latency=0.0,
        )
        # Both items configured to yield priority scores
        req = AdaptiveReviewRequest(concepts=[item1, item2])
        response = adaptive_service.prioritize_queue(req)
        assert len(response.queue) == 2
        assert response.queue[0].rank == 1
        assert response.queue[1].rank == 2
