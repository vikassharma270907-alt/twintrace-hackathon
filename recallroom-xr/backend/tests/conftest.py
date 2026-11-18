"""
Pytest configuration and shared fixtures for RecallRoom XR backend tests.
"""

import io
import math
import struct
import wave
import pytest
from fastapi.testclient import TestClient

from app.main import app
from app.schemas.requests import ConceptReviewItem


@pytest.fixture(scope="session")
def client():
    """FastAPI TestClient session fixture."""
    with TestClient(app) as test_client:
        yield test_client


@pytest.fixture
def synthetic_wav_bytes():
    """
    Generates a valid 1.0-second 16kHz mono 16-bit PCM WAV in memory.
    """
    sample_rate = 16000
    duration = 1.0  # seconds
    frequency = 440.0  # Hz
    num_samples = int(sample_rate * duration)

    buf = io.BytesIO()
    with wave.open(buf, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        
        frames = bytearray()
        for i in range(num_samples):
            # 440 Hz sine wave
            val = int(16000.0 * math.sin(2.0 * math.pi * frequency * (i / sample_rate)))
            frames.extend(struct.pack("<h", val))
        
        wf.writeframes(frames)
    
    return buf.getvalue()


@pytest.fixture
def sample_concept_items():
    """Sample diverse concept items for adaptive scheduler tests."""
    return [
        ConceptReviewItem(
            concept_id="net_tcp_01",
            concept_title="TCP Three-Way Handshake",
            recall_score=0.20,       # Struggled heavily
            time_since_review=0.85,  # Long time elapsed
            hint_dependency=0.60,    # Needed hints
            response_latency=0.70,   # Hesitated
            review_count=3
        ),
        ConceptReviewItem(
            concept_id="ml_backprop_02",
            concept_title="Backpropagation and Gradient Descent",
            recall_score=0.90,       # High mastery
            time_since_review=0.10,  # Just reviewed
            hint_dependency=0.00,    # Zero hints
            response_latency=0.15,   # Fast recall
            review_count=8
        ),
        ConceptReviewItem(
            concept_id="solar_jupiter_03",
            concept_title="Great Red Spot of Jupiter",
            recall_score=0.55,       # Moderate recall
            time_since_review=0.50,  # Medium time elapsed
            hint_dependency=0.30,    # Moderate hints
            response_latency=0.40,   # Normal latency
            review_count=2
        ),
        ConceptReviewItem(
            concept_id="net_dns_04",
            concept_title="DNS Recursive Resolution",
            recall_score=0.40,       # Below average recall
            time_since_review=0.70,  # Decent elapsed time
            hint_dependency=0.50,    # Moderate hints
            response_latency=0.60,   # High latency
            review_count=4
        )
    ]
