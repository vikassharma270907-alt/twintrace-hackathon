"""
RecallRoom XR AI Backend - FastAPI Application Entrypoint.
Provides speech-to-text transcription, semantic recall evaluation, and adaptive spaced repetition scheduling.
"""

import time
import logging
from contextlib import asynccontextmanager
from typing import Callable

from fastapi import FastAPI, Request, status
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from starlette.exceptions import HTTPException as StarletteHTTPException
from fastapi.middleware.cors import CORSMiddleware

from app.config import settings
from app.api.endpoints import router as api_router
from app.services.asr_service import asr_service
from app.services.semantic_service import semantic_service

# Configure structured logging
logging.basicConfig(
    level=logging.DEBUG if settings.DEBUG else logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)
logger = logging.getLogger("recallroom.main")


@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Application lifespan context manager for startup and shutdown events.
    """
    logger.info(f"Starting {settings.APP_NAME} v{settings.APP_VERSION}...")
    logger.info(f"ASR Engine: {asr_service.engine_name} (Loaded: {asr_service.is_model_loaded()})")
    logger.info(f"Semantic Engine: {semantic_service.engine_name} (Loaded: {semantic_service.is_model_loaded()})")
    yield
    logger.info(f"Shutting down {settings.APP_NAME}...")


def create_app() -> FastAPI:
    """
    Application factory for RecallRoom XR backend.
    """
    app = FastAPI(
        title="RecallRoom XR AI Backend",
        description=(
            "AI microservice powering RecallRoom XR Meta Quest 3 spatial learning system: "
            "ASR audio transcription, semantic cosine similarity scoring with 3-tier policy, "
            "and deterministic spaced repetition adaptive scheduling."
        ),
        version=settings.APP_VERSION,
        lifespan=lifespan,
        docs_url="/docs",
        redoc_url="/redoc"
    )

    # --------------------------------------------------------------------------
    # CORS Middleware
    # --------------------------------------------------------------------------
    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.CORS_ORIGINS,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    # --------------------------------------------------------------------------
    # Request Timing and Logging Middleware
    # --------------------------------------------------------------------------
    @app.middleware("http")
    async def log_requests(request: Request, call_next: Callable):
        start_time = time.time()
        path = request.url.path
        method = request.method
        response = await call_next(request)
        process_time = (time.time() - start_time) * 1000.0
        response.headers["X-Process-Time-Ms"] = f"{process_time:.2f}"
        logger.info(f"{method} {path} -> status={response.status_code} ({process_time:.2f}ms)")
        return response

    # --------------------------------------------------------------------------
    # Exception Handlers
    # --------------------------------------------------------------------------
    @app.exception_handler(RequestValidationError)
    async def validation_exception_handler(request: Request, exc: RequestValidationError):
        logger.warning(f"Validation error on {request.method} {request.url.path}: {exc.errors()}")
        return JSONResponse(
            status_code=422,
            content={
                "error": "Validation Error",
                "details": exc.errors(),
                "path": str(request.url.path)
            }
        )

    @app.exception_handler(StarletteHTTPException)
    async def http_exception_handler(request: Request, exc: StarletteHTTPException):
        logger.warning(f"HTTP exception on {request.method} {request.url.path}: {exc.status_code} - {exc.detail}")
        return JSONResponse(
            status_code=exc.status_code,
            content={
                "error": exc.detail,
                "status_code": exc.status_code,
                "path": str(request.url.path)
            }
        )

    @app.exception_handler(Exception)
    async def unhandled_exception_handler(request: Request, exc: Exception):
        logger.error(f"Unhandled exception on {request.method} {request.url.path}: {exc}", exc_info=True)
        return JSONResponse(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            content={
                "error": "Internal Server Error",
                "message": str(exc),
                "path": str(request.url.path)
            }
        )

    # --------------------------------------------------------------------------
    # Root Health Redirect / Aliases & API Routes
    # --------------------------------------------------------------------------
    @app.get("/", tags=["Root"])
    async def root():
        return {
            "name": settings.APP_NAME,
            "version": settings.APP_VERSION,
            "status": "online",
            "docs": "/docs",
            "health": "/health"
        }

    # Include the primary API router
    app.include_router(api_router)

    return app


app = create_app()

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app.main:app", host=settings.HOST, port=settings.PORT, reload=settings.DEBUG)
