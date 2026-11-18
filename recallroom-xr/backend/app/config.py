"""
Configuration settings for RecallRoom XR Backend.
"""

import os
from typing import List
from pydantic import BaseModel, Field


class Settings(BaseModel):
    APP_NAME: str = "recallroom-backend"
    APP_VERSION: str = "1.0.0"
    DEBUG: bool = Field(default_factory=lambda: os.getenv("DEBUG", "false").lower() in ("true", "1", "yes"))
    
    # Server configuration
    HOST: str = Field(default_factory=lambda: os.getenv("HOST", "0.0.0.0"))
    PORT: int = Field(default_factory=lambda: int(os.getenv("PORT", "8000")))
    CORS_ORIGINS: List[str] = ["*"]
    
    # ASR Configuration
    ASR_MODEL: str = Field(default_factory=lambda: os.getenv("ASR_MODEL", "tiny.en"))
    ASR_FORCE_MOCK: bool = Field(default_factory=lambda: os.getenv("ASR_FORCE_MOCK", "false").lower() in ("true", "1", "yes"))
    
    # Semantic Evaluation Configuration
    SEMANTIC_MODEL: str = Field(default_factory=lambda: os.getenv("SEMANTIC_MODEL", "all-MiniLM-L6-v2"))
    THRESHOLD_CORRECT: float = Field(default_factory=lambda: float(os.getenv("THRESHOLD_CORRECT", "0.75")))
    THRESHOLD_PARTIAL: float = Field(default_factory=lambda: float(os.getenv("THRESHOLD_PARTIAL", "0.50")))
    
    # Adaptive Spaced Repetition Weights
    WEIGHT_RECALL: float = 0.45
    WEIGHT_TIME: float = 0.25
    WEIGHT_HINT: float = 0.20
    WEIGHT_LATENCY: float = 0.10


settings = Settings()
