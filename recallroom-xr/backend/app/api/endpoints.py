"""
API Endpoints for RecallRoom XR AI Backend.
"""

import json
import base64
import logging
from typing import Optional, List
from fastapi import APIRouter, Request, File, UploadFile, Form, Header, HTTPException, Query, status

from app.config import settings
from app.schemas.requests import (
    EvaluationRequest,
    AdaptiveReviewRequest,
)
from app.schemas.responses import (
    EvaluationResponse,
    TranscribeResponse,
    AudioEvaluationResponse,
    AdaptiveReviewResponse,
    HealthResponse,
    VersionResponse,
)
from app.services.asr_service import asr_service
from app.services.semantic_service import semantic_service
from app.services.adaptive_service import adaptive_service

logger = logging.getLogger("recallroom.api")
router = APIRouter()


# --------------------------------------------------------------------------
# Health and Version Endpoints
# --------------------------------------------------------------------------

@router.get("/health", response_model=HealthResponse, tags=["Health"])
async def health_check():
    """
    Returns service health, readiness, and active model state.
    """
    asr_info = asr_service.get_info()
    sem_info = semantic_service.get_info()
    models_ready = bool(asr_service.engine_name and semantic_service.engine_name)

    return HealthResponse(
        status="healthy",
        service=settings.APP_NAME,
        models_loaded=models_ready,
        details={
            "asr": asr_info,
            "semantic": sem_info,
            "version": settings.APP_VERSION,
        }
    )


@router.get("/api/version", response_model=VersionResponse, tags=["System"])
async def get_version():
    """
    Returns current API version, engine metadata, and runtime device.
    """
    return VersionResponse(
        version=settings.APP_VERSION,
        service=settings.APP_NAME,
        models={
            "asr_engine": asr_service.engine_name,
            "asr_model": asr_service.model_name,
            "semantic_engine": semantic_service.engine_name,
            "semantic_model": semantic_service.model_name,
        },
        device="cpu"
    )


# --------------------------------------------------------------------------
# Speech-To-Text (ASR)
# --------------------------------------------------------------------------

@router.post("/api/transcribe", response_model=TranscribeResponse, tags=["ASR"])
async def transcribe_audio(
    request: Request,
    file: Optional[UploadFile] = File(None),
    x_mock_transcript: Optional[str] = Header(None, alias="X-Mock-Transcript"),
    mock_transcript: Optional[str] = Query(None),
):
    """
    Transcribes student verbal recall audio (WAV format).
    Supports either multipart form file upload or base64-encoded audio payload in JSON.
    """
    audio_bytes: bytes = b""
    filename: Optional[str] = None
    language: str = "en"

    content_type = request.headers.get("content-type", "")

    if "application/json" in content_type:
        try:
            body = await request.json()
            b64_audio = body.get("audio_base64")
            language = body.get("language", "en")
            if b64_audio:
                audio_bytes = base64.b64decode(b64_audio)
                filename = "payload.wav"
        except Exception as exc:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail=f"Invalid JSON/base64 audio payload: {exc}"
            )
    elif file is not None:
        filename = file.filename
        audio_bytes = await file.read()

    if not audio_bytes:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Either multipart WAV file upload or JSON 'audio_base64' is required."
        )

    # Determine any explicit mock transcript passed for testing/fallback
    effective_mock = mock_transcript or x_mock_transcript

    response = asr_service.transcribe(
        audio_bytes=audio_bytes,
        filename=filename,
        mock_transcript=effective_mock,
        language=language
    )
    return response


# --------------------------------------------------------------------------
# Semantic Evaluation
# --------------------------------------------------------------------------

@router.post("/api/evaluate", response_model=EvaluationResponse, tags=["Evaluation"])
async def evaluate_recall(request: EvaluationRequest):
    """
    Evaluates student verbal recall answer against expected answer and keywords.
    Applies calibrated 3-tier policy:
      - CORRECT (>= 0.75)
      - PARTIAL (0.50 - 0.74)
      - RETRY   (< 0.50)
    """
    if not request.concept_title or not request.expected_answer:
        raise HTTPException(
            status_code=422,
            detail="concept_title and expected_answer cannot be empty."
        )
    return semantic_service.evaluate(request)


# --------------------------------------------------------------------------
# Combined Audio Upload + Transcription + Evaluation
# --------------------------------------------------------------------------

@router.post("/api/evaluate-audio", response_model=AudioEvaluationResponse, tags=["Evaluation"])
async def evaluate_audio(
    file: UploadFile = File(..., description="Recorded WAV audio from Quest headset"),
    concept_id: str = Form(...),
    concept_title: str = Form(...),
    expected_answer: str = Form(...),
    accepted_answers: Optional[str] = Form(None, description="JSON array or comma-separated list"),
    keywords: Optional[str] = Form(None, description="Comma-separated or JSON array of key terms"),
    x_mock_transcript: Optional[str] = Header(None, alias="X-Mock-Transcript"),
    mock_transcript: Optional[str] = Form(None),
):
    """
    End-to-end endpoint: receives microphone WAV audio, transcribes speech,
    and runs semantic evaluation against the concept definition.
    """
    audio_bytes = await file.read()
    if not audio_bytes:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Audio file content is empty."
        )

    # Transcribe speech
    effective_mock = mock_transcript or x_mock_transcript
    asr_res = asr_service.transcribe(
        audio_bytes=audio_bytes,
        filename=file.filename,
        mock_transcript=effective_mock
    )

    # Parse accepted answers
    accepted_list: List[str] = []
    if accepted_answers:
        try:
            parsed = json.loads(accepted_answers)
            if isinstance(parsed, list):
                accepted_list = [str(x) for x in parsed]
            else:
                accepted_list = [str(accepted_answers)]
        except Exception:
            accepted_list = [x.strip() for x in accepted_answers.split(",") if x.strip()]

    # Parse keywords
    keywords_list: List[str] = []
    if keywords:
        try:
            parsed = json.loads(keywords)
            if isinstance(parsed, list):
                keywords_list = [str(x) for x in parsed]
            else:
                keywords_list = [str(keywords)]
        except Exception:
            keywords_list = [k.strip() for k in keywords.split(",") if k.strip()]

    # Build evaluation request
    eval_req = EvaluationRequest(
        concept_id=concept_id,
        concept_title=concept_title,
        expected_answer=expected_answer,
        student_transcript=asr_res.transcript,
        accepted_answers=accepted_list,
        keywords=keywords_list
    )

    eval_res = semantic_service.evaluate(eval_req)

    return AudioEvaluationResponse(
        transcript=asr_res.transcript,
        evaluation=eval_res,
        audio_duration=asr_res.duration_seconds
    )


# --------------------------------------------------------------------------
# Adaptive Spaced Repetition Scheduling
# --------------------------------------------------------------------------

@router.post("/api/review/next", response_model=AdaptiveReviewResponse, tags=["Adaptive"])
async def schedule_next_reviews(request: AdaptiveReviewRequest):
    """
    Calculates deterministic priority schedule for concept cards:
      priority = 0.45 * (1 - recall) + 0.25 * time + 0.20 * hint + 0.10 * latency
    Returns prioritized review queue with ranks and explanatory rationales.
    """
    if not request.concepts:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="At least one concept is required in the review list."
        )
    return adaptive_service.prioritize_queue(request)
