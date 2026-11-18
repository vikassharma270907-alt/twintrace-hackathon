"""
Request schemas for RecallRoom XR AI API.
"""

from typing import List, Optional
from pydantic import BaseModel, Field


class EvaluationRequest(BaseModel):
    concept_id: str = Field(..., description="Unique concept identifier, e.g. 'net_01'")
    concept_title: str = Field(..., description="Concept title, e.g. 'TCP Three-Way Handshake'")
    expected_answer: str = Field(..., description="Canonical reference explanation or definition")
    student_transcript: str = Field(..., description="Transcribed student verbal recall answer")
    accepted_answers: List[str] = Field(
        default_factory=list,
        description="Alternative acceptable descriptions or formulations"
    )
    keywords: List[str] = Field(
        default_factory=list,
        description="Key vocabulary terms expected in the recall response"
    )
    threshold_correct: Optional[float] = Field(
        default=None,
        description="Override threshold for CORRECT tier (default: 0.75)"
    )
    threshold_partial: Optional[float] = Field(
        default=None,
        description="Override threshold for PARTIAL tier (default: 0.50)"
    )


class TranscribeRequest(BaseModel):
    audio_base64: Optional[str] = Field(
        default=None,
        description="Optional base64-encoded WAV audio content"
    )
    language: str = Field(default="en", description="Target language code (default 'en')")


class ConceptReviewItem(BaseModel):
    concept_id: str = Field(..., description="Concept identifier")
    concept_title: str = Field(..., description="Human-readable concept name")
    recall_score: float = Field(
        ...,
        ge=0.0,
        le=1.0,
        description="Normalized historical recall score [0.0 = total failure, 1.0 = flawless recall]"
    )
    time_since_review: float = Field(
        ...,
        ge=0.0,
        le=1.0,
        description="Normalized elapsed time factor since last review [0.0 = just reviewed, 1.0 = long elapsed]"
    )
    hint_dependency: float = Field(
        ...,
        ge=0.0,
        le=1.0,
        description="Fraction of hints utilized during past attempts [0.0 = no hints, 1.0 = heavy hint reliance]"
    )
    response_latency: float = Field(
        ...,
        ge=0.0,
        le=1.0,
        description="Normalized hesitation or retrieval latency [0.0 = instant, 1.0 = prolonged hesitation]"
    )
    review_count: int = Field(default=0, ge=0, description="Total number of historical reviews")
    deck_id: Optional[str] = Field(default=None, description="Optional study deck identifier")


class AdaptiveReviewRequest(BaseModel):
    concepts: List[ConceptReviewItem] = Field(
        ...,
        min_length=1,
        description="List of student concept recall records"
    )
    top_k: Optional[int] = Field(
        default=None,
        ge=1,
        description="Optional limit on the number of prioritized concepts returned"
    )
