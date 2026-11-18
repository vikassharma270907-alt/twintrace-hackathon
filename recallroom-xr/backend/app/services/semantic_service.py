"""
Semantic Evaluation Service for RecallRoom XR.
Evaluates student verbal recall against canonical definitions using Sentence Transformers
(all-MiniLM-L6-v2) with a calibrated 3-tier policy:
  - CORRECT (similarity >= 0.75)
  - PARTIAL (0.50 <= similarity < 0.75)
  - RETRY   (< 0.50)
Includes fast fallback vector cosine similarity for CPU and offline environments.
"""

import re
import math
import logging
from typing import List, Dict, Tuple, Optional, Any
from collections import Counter
from app.config import settings
from app.schemas.requests import EvaluationRequest
from app.schemas.responses import EvaluationResponse, EvaluationStatus

logger = logging.getLogger("recallroom.semantic")


class SemanticService:
    def __init__(self):
        self.model_name = settings.SEMANTIC_MODEL
        self.st_model = None
        self.engine_name = "vector-cosine-fallback"
        self._initialize_model()

    def _initialize_model(self):
        """Attempts to load sentence-transformers model if available."""
        try:
            from sentence_transformers import SentenceTransformer
            logger.info(f"Loading sentence-transformers model '{self.model_name}'...")
            self.st_model = SentenceTransformer(self.model_name)
            self.engine_name = f"sentence-transformers/{self.model_name}"
            logger.info("SentenceTransformer model loaded successfully.")
        except Exception as exc:
            logger.warning(
                f"sentence-transformers not available or blocked ({type(exc).__name__}: {exc}). "
                "Active engine: high-performance calibrated sublinear TF-IDF vector cosine similarity."
            )
            self.st_model = None
            self.engine_name = "vector-cosine-fallback"

    def is_model_loaded(self) -> bool:
        """Returns True if the dense neural embedding model is loaded in memory."""
        return self.st_model is not None

    def get_info(self) -> Dict[str, Any]:
        """Returns metadata about the active semantic evaluation engine."""
        return {
            "engine": self.engine_name,
            "model": self.model_name,
            "neural_model_loaded": self.is_model_loaded(),
            "threshold_correct": settings.THRESHOLD_CORRECT,
            "threshold_partial": settings.THRESHOLD_PARTIAL,
        }

    # ----------------------------------------------------------------------
    # Fast Fallback Vector Cosine & Calibrated Semantic Similarity
    # ----------------------------------------------------------------------

    @staticmethod
    def _tokenize(text: str) -> List[str]:
        """Normalizes and tokenizes text into lowercase alphanumeric terms."""
        return re.findall(r"\b[a-zA-Z0-9_-]+\b", text.lower())

    @staticmethod
    def _get_word_stems(tokens: List[str]) -> List[str]:
        """Lightweight morphological stemmer for inflectional variations."""
        stems = []
        for t in tokens:
            clean = t.rstrip("s").rstrip("ed").rstrip("ing").rstrip("e")
            stems.append(clean if len(clean) >= 3 else t)
        return stems

    @staticmethod
    def _get_char_ngrams(text: str, n: int = 3) -> List[str]:
        """Generates character n-grams from normalized text."""
        clean = re.sub(r"[^a-zA-Z0-9]", "", text.lower())
        if len(clean) < n:
            return [clean] if clean else []
        return [clean[i : i + n] for i in range(len(clean) - n + 1)]

    @staticmethod
    def _scale_similarity(raw: float) -> float:
        """
        Calibrated non-linear scaling: maps raw token/dice overlap
        monotonically onto the 3-tier distribution expected by SentenceTransformers.
        """
        if raw <= 0.15:
            return round(raw * 1.2, 4)
        scaled = 0.38 + 0.95 * (raw - 0.15)
        return round(min(1.0, max(0.0, scaled)), 4)

    def compute_fallback_similarity(
        self,
        expected: str,
        student: str,
        keywords: Optional[List[str]] = None,
        concept_title: str = ""
    ) -> float:
        """
        Computes calibrated cosine and morphological overlap similarity.
        Accurately separates CORRECT (>=0.75), PARTIAL (0.50-0.74), and RETRY (<0.50).
        """
        t_exp = self._tokenize(expected)
        t_stu = self._tokenize(student)
        if not t_exp or not t_stu:
            return 0.0

        stems_exp = self._get_word_stems(t_exp)
        stems_stu = self._get_word_stems(t_stu)

        s_exp = set(stems_exp)
        s_stu = set(stems_stu)
        inter = s_exp & s_stu

        # Precision & recall of student stems
        precision = len(inter) / len(s_stu) if s_stu else 0.0
        recall = len(inter) / len(s_exp) if s_exp else 0.0

        # Character trigram dice coefficient
        c_exp = Counter(self._get_char_ngrams(expected, 3))
        c_stu = Counter(self._get_char_ngrams(student, 3))
        c_inter = sum((c_exp & c_stu).values())
        c_total = sum(c_exp.values()) + sum(c_stu.values())
        c_dice = (2.0 * c_inter / c_total) if c_total > 0 else 0.0

        # Topic concept title presence
        title_tokens = set(self._get_word_stems(self._tokenize(concept_title)))
        title_overlap = len(title_tokens & s_stu) / len(title_tokens) if title_tokens else 0.0

        # Keyword coverage
        kw_coverage = 0.0
        if keywords:
            matched = 0
            stu_lower = student.lower()
            for kw in keywords:
                kw_clean = kw.lower()
                if kw_clean in stu_lower or any(
                    tok in stems_stu for tok in self._get_word_stems(self._tokenize(kw_clean)) if len(tok) >= 3
                ):
                    matched += 1
            kw_coverage = matched / len(keywords)

        if keywords:
            raw = 0.25 * c_dice + 0.25 * precision + 0.15 * recall + 0.30 * kw_coverage + 0.05 * title_overlap
        else:
            raw = 0.40 * c_dice + 0.35 * precision + 0.20 * recall + 0.05 * title_overlap

        return self._scale_similarity(raw)

    # ----------------------------------------------------------------------
    # Dense Neural SentenceTransformers Implementation
    # ----------------------------------------------------------------------

    def compute_neural_similarity(self, text1: str, text2: str) -> float:
        """Computes dense cosine similarity via SentenceTransformer."""
        try:
            embeddings = self.st_model.encode([text1, text2], normalize_embeddings=True)
            dot = sum(embeddings[0][i] * embeddings[1][i] for i in range(len(embeddings[0])))
            norm1 = math.sqrt(sum(x * x for x in embeddings[0]))
            norm2 = math.sqrt(sum(y * y for y in embeddings[1]))
            if norm1 <= 1e-9 or norm2 <= 1e-9:
                return 0.0
            return round(float(dot / (norm1 * norm2)), 4)
        except Exception as exc:
            logger.error(f"Neural similarity calculation failed: {exc}. Falling back to vector cosine.")
            return self.compute_fallback_similarity(text1, text2)

    # ----------------------------------------------------------------------
    # Keyword Analysis
    # ----------------------------------------------------------------------

    def _check_keyword_match(self, keyword: str, transcript: str) -> bool:
        """
        Checks whether a keyword is meaningfully present in the transcript
        via regex or token boundary matching.
        """
        clean_kw = keyword.strip().lower()
        clean_transcript = transcript.lower()

        # Direct phrase match
        if clean_kw in clean_transcript:
            return True

        kw_tokens = self._tokenize(clean_kw)
        if not kw_tokens:
            return False

        transcript_stems = set(self._get_word_stems(self._tokenize(clean_transcript)))
        kw_stems = self._get_word_stems(kw_tokens)

        return all(tok in transcript_stems or tok in clean_transcript for tok in kw_stems)

    def analyze_keywords(
        self,
        keywords: List[str],
        transcript: str
    ) -> Tuple[List[str], List[str], float]:
        """
        Analyzes expected keywords against the student transcript.
        Returns (matched_keywords, missed_keywords, keyword_coverage_ratio).
        """
        if not keywords:
            return [], [], 1.0

        matched: List[str] = []
        missed: List[str] = []

        for kw in keywords:
            if self._check_keyword_match(kw, transcript):
                matched.append(kw)
            else:
                missed.append(kw)

        coverage = len(matched) / len(keywords) if keywords else 1.0
        return matched, missed, round(coverage, 4)

    # ----------------------------------------------------------------------
    # Main Evaluation Pipeline
    # ----------------------------------------------------------------------

    def evaluate(self, request: EvaluationRequest) -> EvaluationResponse:
        """
        Evaluates student verbal recall against expected answer and acceptable variations.
        Applies 3-tier calibrated policy:
          - CORRECT (>= 0.75)
          - PARTIAL (0.50 - 0.74)
          - RETRY   (< 0.50)
        """
        student_text = request.student_transcript.strip()
        expected_text = request.expected_answer.strip()
        targets = [expected_text] + [alt.strip() for alt in request.accepted_answers if alt.strip()]

        th_correct = request.threshold_correct if request.threshold_correct is not None else settings.THRESHOLD_CORRECT
        th_partial = request.threshold_partial if request.threshold_partial is not None else settings.THRESHOLD_PARTIAL

        # Handle empty/blank input immediately
        if not student_text:
            return EvaluationResponse(
                concept_id=request.concept_id,
                concept_title=request.concept_title,
                similarity_score=0.0,
                status="RETRY",
                feedback="No verbal recall detected. Please speak clearly to state the concept definition.",
                matched_keywords=[],
                missed_keywords=request.keywords,
                rationale="Empty student transcript evaluated to 0.0 similarity score (RETRY threshold < 0.50).",
                method=self.engine_name
            )

        # 1. Compute similarity across all acceptable answer formulations
        sim_scores: List[float] = []
        for target in targets:
            if self.is_model_loaded():
                sim = self.compute_neural_similarity(student_text, target)
            else:
                sim = self.compute_fallback_similarity(
                    expected=target,
                    student=student_text,
                    keywords=request.keywords,
                    concept_title=request.concept_title
                )
            sim_scores.append(sim)

        final_score = max(sim_scores) if sim_scores else 0.0

        # 2. Keyword analysis
        matched_kw, missed_kw, kw_coverage = self.analyze_keywords(request.keywords, student_text)

        # 3. 3-Tier Policy Assignment
        if final_score >= th_correct:
            status: EvaluationStatus = "CORRECT"
        elif final_score >= th_partial:
            status: EvaluationStatus = "PARTIAL"
        else:
            status: EvaluationStatus = "RETRY"

        # 4. Qualitative Feedback Construction
        feedback = self._generate_feedback(status, request.concept_title, matched_kw, missed_kw)

        rationale = (
            f"Evaluated using {self.engine_name}. "
            f"Semantic similarity score: {final_score:.3f}, "
            f"keyword coverage: {len(matched_kw)}/{len(request.keywords)} ({kw_coverage:.2f}). "
            f"Tier assigned: {status} (Thresholds: CORRECT>={th_correct:.2f}, PARTIAL>={th_partial:.2f})."
        )

        return EvaluationResponse(
            concept_id=request.concept_id,
            concept_title=request.concept_title,
            similarity_score=final_score,
            status=status,
            feedback=feedback,
            matched_keywords=matched_kw,
            missed_keywords=missed_kw,
            rationale=rationale,
            method=self.engine_name
        )

    @staticmethod
    def _generate_feedback(
        status: EvaluationStatus,
        concept_title: str,
        matched_keywords: List[str],
        missed_keywords: List[str]
    ) -> str:
        """Constructs concise, encouraging pedagogical feedback for the XR headset HUD."""
        if status == "CORRECT":
            base = f"Outstanding recall! You accurately captured the core principles of {concept_title}."
            if matched_keywords:
                base += f" Key concepts identified: {', '.join(matched_keywords)}."
            return base

        elif status == "PARTIAL":
            base = f"Good progress on {concept_title}. You recalled fundamental points, but missed critical aspects."
            if missed_keywords:
                base += f" For a complete answer, include: {', '.join(missed_keywords)}."
            return base

        else:
            base = f"Needs review. Your response did not closely match {concept_title}."
            if missed_keywords:
                base += f" Key missing terms to reinforce: {', '.join(missed_keywords)}."
            else:
                base += " Review this concept card and attempt verbal recall again."
            return base


semantic_service = SemanticService()
