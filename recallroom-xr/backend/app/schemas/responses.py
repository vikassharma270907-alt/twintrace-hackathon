"""
Response schemas for RecallRoom XR AI API.
"""

from typing import Dict, List, Literal, Optional, Any
from pydantic import BaseModel, Field
from app.schemas.requests import ConceptReviewItem


EvaluationStatus = Literal["CORRECT", "PARTIAL", "RETRY"]


class EvaluationResponse(BaseModel):
    concept_id: str
    concept_title: str
    similarity_score: float = Field(..., description="Cosine similarity score [0.0 - 1.0]")
    status: EvaluationStatus = Field(..., description="Tier: CORRECT (>=0.75), PARTIAL (0.50-0.74), RETRY (<0.50)")
    feedback: str = Field(..., description="Actionable qualitative pedagogical feedback")
    matched_keywords: List[str] = Field(default_factory=list, description="Keywords present in transcript")
    missed_keywords: List[str] = Field(default_factory=list, description="Expected keywords missing from transcript")
    rationale: str = Field(..., description="Explanation of the scoring calculation and calibration")
    method: str = Field(..., description="Evaluation engine used (e.g. 'sentence-transformers' or 'vector-cosine-fallback')")


class TranscribeResponse(BaseModel):
    transcript: str = Field(..., description="Transcribed text from speech")
    language: str = Field(default="en")
    duration_seconds: float = Field(default=0.0, description="Audio duration in seconds")
    confidence: float = Field(default=1.0, description="Transcription confidence score [0.0 - 1.0]")
    engine: str = Field(default="whisper", description="ASR engine used (e.g. 'faster-whisper', 'mock-wav-parser')")


class AudioEvaluationResponse(BaseModel):
    transcript: str
    evaluation: EvaluationResponse
    audio_duration: float


class PriorityBreakdown(BaseModel):
    recall_factor: float = Field(..., description="0.45 * (1 - recall_score)")
    time_factor: float = Field(..., description="0.25 * time_since_review")
    hint_factor: float = Field(..., description="0.20 * hint_dependency")
    latency_factor: float = Field(..., description="0.10 * response_latency")


class PrioritizedConcept(BaseModel):
    concept_id: str
    concept_title: str
    priority_score: float = Field(..., description="Calculated priority score [0.0 - 1.0]")
    rank: int = Field(..., description="1-based position in review schedule queue")
    breakdown: PriorityBreakdown
    rationale: str = Field(..., description="Deterministic explanation of why this concept is prioritized")
    original_data: ConceptReviewItem


class AdaptiveReviewResponse(BaseModel):
    queue: List[PrioritizedConcept]
    total_concepts: int
    top_recommendation: Optional[str] = None


class HealthResponse(BaseModel):
    status: str
    service: str
    models_loaded: bool
    details: Optional[Dict[str, Any]] = None


class VersionResponse(BaseModel):
    version: str
    service: str
    models: Dict[str, str]
    device: str
