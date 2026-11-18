"""
End-to-End Integration Test for RecallRoom XR.
Simulates the Unity C# client (RecallRoomApiClient) calling all FastAPI endpoints:
- GET /health
- GET /api/version
- POST /api/transcribe (multipart WAV upload via WWWForm)
- POST /api/evaluate (using real study deck concept cards)
- POST /api/evaluate-audio (combined ASR + semantic evaluation)
- POST /api/review/next (adaptive scheduling of 5 concept cards)
"""

import io
import json
import wave
import struct
import pytest
from starlette.testclient import TestClient
from app.main import app


@pytest.fixture(scope="module")
def client():
    return TestClient(app)


@pytest.fixture(scope="module")
def sample_wav_bytes():
    """Generates a 1-second 16kHz mono PCM WAV file in memory."""
    buf = io.BytesIO()
    with wave.open(buf, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(16000)
        samples = [0] * 16000
        wav.writeframes(struct.pack("<" + "h" * len(samples), *samples))
    return buf.getvalue()


class TestUnityClientIntegration:

    def test_e2e_health_check(self, client: TestClient):
        """Simulates Unity initial connectivity probe on startup."""
        resp = client.get("/health")
        assert resp.status_code == 200
        data = resp.json()
        assert data["status"] == "healthy"
        assert data["service"] == "recallroom-backend"
        assert data["models_loaded"] is True
        assert "asr" in data["details"]
        assert "semantic" in data["details"]

    def test_e2e_version_metadata(self, client: TestClient):
        """Simulates Unity checking server version compatibility."""
        resp = client.get("/api/version")
        assert resp.status_code == 200
        data = resp.json()
        assert data["version"] == "1.0.0"
        assert "asr_engine" in data["models"]
        assert "semantic_engine" in data["models"]

    def test_e2e_transcribe_multipart_form(self, client: TestClient, sample_wav_bytes: bytes):
        """Simulates Unity WWWForm binary upload of recorded mic audio."""
        files = {"file": ("recording.wav", sample_wav_bytes, "audio/wav")}
        headers = {"X-Mock-Transcript": "The TCP three-way handshake establishes a connection using SYN and ACK."}
        resp = client.post("/api/transcribe", files=files, headers=headers)
        assert resp.status_code == 200
        data = resp.json()
        assert "transcript" in data
        assert len(data["transcript"]) > 0
        assert data["duration_seconds"] > 0
        assert 0.0 <= data["confidence"] <= 1.0

    def test_e2e_evaluate_networking_card(self, client: TestClient):
        """Simulates Unity RecallRoomApiClient.EvaluateRecallAsync with Networking Basics deck."""
        payload = {
            "concept_id": "net_tcp_handshake",
            "concept_title": "TCP 3-Way Handshake",
            "expected_answer": "The TCP three-way handshake establishes a reliable connection using SYN, SYN-ACK, and ACK packets between client and server.",
            "student_transcript": "The TCP three-way handshake establishes a reliable connection using SYN, SYN-ACK, and ACK packets.",
            "accepted_answers": [
                "A 3-step synchronization process where client sends SYN, server replies SYN-ACK, client finishes with ACK."
            ],
            "keywords": ["SYN", "SYN-ACK", "ACK", "handshake", "synchronize", "connection establishment"]
        }
        resp = client.post("/api/evaluate", json=payload)
        assert resp.status_code == 200
        data = resp.json()
        assert data["concept_id"] == "net_tcp_handshake"
        assert data["status"] in ("CORRECT", "PARTIAL")
        assert data["similarity_score"] >= 0.70
        assert "SYN" in data["matched_keywords"]
        assert "ACK" in data["matched_keywords"]

    def test_e2e_evaluate_audio_combined(self, client: TestClient, sample_wav_bytes: bytes):
        """Simulates Unity combined audio + evaluation endpoint."""
        files = {"file": ("mic.wav", sample_wav_bytes, "audio/wav")}
        data = {
            "concept_id": "net_osi_layers",
            "concept_title": "OSI 7-Layer Model",
            "expected_answer": "Standard theoretical framework dividing network communications into 7 layers from Physical to Application.",
            "keywords": json.dumps(["Physical", "Data Link", "Network", "Transport", "Session", "Presentation", "Application"])
        }
        resp = client.post("/api/evaluate-audio", files=files, data=data)
        assert resp.status_code == 200
        result = resp.json()
        assert "transcript" in result
        assert "evaluation" in result
        assert result["evaluation"]["concept_id"] == "net_osi_layers"

    def test_e2e_adaptive_review_deck_scheduling(self, client: TestClient):
        """Simulates Unity AdaptiveScheduler requesting optimal review order for all 5 deck cards."""
        # 5 cards with varying simulated historical retention metrics
        concepts = [
            {
                "concept_id": "card_01",
                "concept_title": "OSI 7-Layer Model",
                "recall_score": 0.20,
                "time_since_review": 0.90,
                "hint_dependency": 0.80,
                "response_latency": 0.85
            },
            {
                "concept_id": "card_02",
                "concept_title": "TCP 3-Way Handshake",
                "recall_score": 0.95,
                "time_since_review": 0.10,
                "hint_dependency": 0.05,
                "response_latency": 0.15
            },
            {
                "concept_id": "card_03",
                "concept_title": "DNS Resolution Hierarchy",
                "recall_score": 0.50,
                "time_since_review": 0.60,
                "hint_dependency": 0.40,
                "response_latency": 0.50
            },
            {
                "concept_id": "card_04",
                "concept_title": "NAT Traversal (STUN/TURN/ICE)",
                "recall_score": 0.35,
                "time_since_review": 0.80,
                "hint_dependency": 0.70,
                "response_latency": 0.75
            },
            {
                "concept_id": "card_05",
                "concept_title": "Subnet Mask & CIDR",
                "recall_score": 0.85,
                "time_since_review": 0.25,
                "hint_dependency": 0.10,
                "response_latency": 0.20
            }
        ]

        resp = client.post("/api/review/next", json={"concepts": concepts})
        assert resp.status_code == 200
        data = resp.json()
        assert data["total_concepts"] == 5
        assert len(data["queue"]) == 5

        # Check that card_01 (lowest recall, longest time, highest hint) is ranked #1
        queue = data["queue"]
        assert queue[0]["concept_id"] == "card_01"
        assert queue[0]["rank"] == 1

        # Check that card_02 (highest recall, lowest time, lowest hint) is ranked last
        assert queue[-1]["concept_id"] == "card_02"
        assert queue[-1]["rank"] == 5

        # Check priority monotonic decrease
        for i in range(len(queue) - 1):
            assert queue[i]["priority_score"] >= queue[i + 1]["priority_score"]
