"""
Integration tests for the RecallRoom XR FastAPI API Endpoints.
Covers all routes:
  - GET  /health
  - GET  /api/version
  - POST /api/transcribe
  - POST /api/evaluate
  - POST /api/evaluate-audio
  - POST /api/review/next
"""

import base64
import pytest
from fastapi.testclient import TestClient


class TestApiEndpoints:
    """Test suite covering FastAPI REST endpoints."""

    def test_root_endpoint(self, client: TestClient):
        """GET / returns system status and documentation paths."""
        resp = client.get("/")
        assert resp.status_code == 200
        data = resp.json()
        assert data["status"] == "online"
        assert "recallroom" in data["name"]

    def test_health_check(self, client: TestClient):
        """GET /health returns healthy status and model readiness."""
        resp = client.get("/health")
        assert resp.status_code == 200
        data = resp.json()
        assert data["status"] == "healthy"
        assert data["service"] == "recallroom-backend"
        assert data["models_loaded"] is True
        assert "asr" in data["details"]
        assert "semantic" in data["details"]

    def test_version_metadata(self, client: TestClient):
        """GET /api/version returns version string and active engines."""
        resp = client.get("/api/version")
        assert resp.status_code == 200
        data = resp.json()
        assert data["version"] == "1.0.0"
        assert "models" in data
        assert "asr_engine" in data["models"]
        assert "semantic_engine" in data["models"]

    def test_transcribe_multipart_wav(self, client: TestClient, synthetic_wav_bytes: bytes):
        """POST /api/transcribe accepts multipart WAV file upload."""
        files = {
            "file": ("test_speech.wav", synthetic_wav_bytes, "audio/wav")
        }
        resp = client.post("/api/transcribe", files=files)
        assert resp.status_code == 200
        data = resp.json()
        assert "transcript" in data
        assert data["duration_seconds"] > 0
        assert data["language"] == "en"

    def test_transcribe_base64_payload(self, client: TestClient, synthetic_wav_bytes: bytes):
        """POST /api/transcribe accepts base64-encoded audio in JSON payload."""
        b64_audio = base64.b64encode(synthetic_wav_bytes).decode("utf-8")
        payload = {
            "audio_base64": b64_audio,
            "language": "en"
        }
        resp = client.post("/api/transcribe", json=payload)
        assert resp.status_code == 200
        data = resp.json()
        assert "transcript" in data
        assert data["confidence"] > 0

    def test_transcribe_mock_header_override(self, client: TestClient, synthetic_wav_bytes: bytes):
        """POST /api/transcribe honors X-Mock-Transcript header for deterministic testing."""
        files = {
            "file": ("test.wav", synthetic_wav_bytes, "audio/wav")
        }
        headers = {
            "X-Mock-Transcript": "The quick brown fox jumps over the lazy dog."
        }
        resp = client.post("/api/transcribe", files=files, headers=headers)
        assert resp.status_code == 200
        data = resp.json()
        assert data["transcript"] == "The quick brown fox jumps over the lazy dog."

    def test_transcribe_missing_audio_error(self, client: TestClient):
        """POST /api/transcribe returns 400 when no audio is sent."""
        resp = client.post("/api/transcribe")
        assert resp.status_code == 400

    def test_evaluate_semantic_recall(self, client: TestClient):
        """POST /api/evaluate evaluates recall against expected answer."""
        payload = {
            "concept_id": "ml_gd_01",
            "concept_title": "Gradient Descent",
            "expected_answer": "An optimization algorithm that iteratively adjusts parameters in the direction of steepest descent to minimize a loss function.",
            "student_transcript": "Gradient descent is an iterative algorithm that updates parameters along the negative gradient of the loss function.",
            "keywords": ["optimization", "gradient", "parameters", "minimize", "loss function"]
        }
        resp = client.post("/api/evaluate", json=payload)
        assert resp.status_code == 200
        data = resp.json()
        assert data["concept_id"] == "ml_gd_01"
        assert data["status"] == "CORRECT"
        assert data["similarity_score"] >= 0.75
        assert len(data["matched_keywords"]) > 0

    def test_evaluate_validation_error(self, client: TestClient):
        """POST /api/evaluate returns 422 when required schema fields are missing."""
        payload = {
            "concept_id": "missing_fields"
            # Missing concept_title, expected_answer, student_transcript
        }
        resp = client.post("/api/evaluate", json=payload)
        assert resp.status_code == 422

    def test_evaluate_audio_end_to_end(self, client: TestClient, synthetic_wav_bytes: bytes):
        """POST /api/evaluate-audio transcribes WAV and performs semantic scoring in one step."""
        files = {
            "file": ("student_voice.wav", synthetic_wav_bytes, "audio/wav")
        }
        data = {
            "concept_id": "net_tcp_01",
            "concept_title": "TCP Three-Way Handshake",
            "expected_answer": "TCP handshake uses SYN, SYN-ACK, and ACK packets to establish a connection.",
            "keywords": '["SYN", "SYN-ACK", "ACK"]',
            "mock_transcript": "TCP handshake uses SYN, SYN-ACK, and ACK packets to establish a connection."
        }
        resp = client.post("/api/evaluate-audio", files=files, data=data)
        assert resp.status_code == 200
        res_data = resp.json()
        assert "transcript" in res_data
        assert res_data["transcript"] == data["mock_transcript"]
        assert "evaluation" in res_data
        assert res_data["evaluation"]["status"] == "CORRECT"
        assert res_data["evaluation"]["similarity_score"] >= 0.75
        assert res_data["audio_duration"] > 0

    def test_adaptive_review_scheduling(self, client: TestClient, sample_concept_items):
        """POST /api/review/next returns prioritized queue sorted by memory decay urgency."""
        payload = {
            "concepts": [item.model_dump() for item in sample_concept_items],
            "top_k": 3
        }
        resp = client.post("/api/review/next", json=payload)
        assert resp.status_code == 200
        data = resp.json()
        assert len(data["queue"]) == 3
        assert data["total_concepts"] == 4
        assert data["queue"][0]["rank"] == 1
        assert data["queue"][0]["priority_score"] >= data["queue"][1]["priority_score"]
        assert "rationale" in data["queue"][0]
        assert data["top_recommendation"] is not None

    def test_adaptive_review_empty_list_error(self, client: TestClient):
        """POST /api/review/next returns 422/400 when empty concept list is submitted."""
        payload = {
            "concepts": []
        }
        resp = client.post("/api/review/next", json=payload)
        assert resp.status_code in (400, 422)
