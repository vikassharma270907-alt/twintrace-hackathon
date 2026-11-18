"""
Adaptive Spaced Repetition Service for RecallRoom XR.
Deterministic prioritization engine implementing the research-calibrated formula:
  priority = 0.45 * (1 - recall_score) + 0.25 * time_since_review + 0.20 * hint_dependency + 0.10 * response_latency
Returns ordered concepts with rank and structured pedagogical rationale explanations.
"""

from typing import List, Optional
from app.config import settings
from app.schemas.requests import ConceptReviewItem, AdaptiveReviewRequest
from app.schemas.responses import (
    PrioritizedConcept,
    PriorityBreakdown,
    AdaptiveReviewResponse,
)


class AdaptiveSchedulerService:
    def __init__(self):
        self.w_recall = settings.WEIGHT_RECALL       # 0.45
        self.w_time = settings.WEIGHT_TIME           # 0.25
        self.w_hint = settings.WEIGHT_HINT           # 0.20
        self.w_latency = settings.WEIGHT_LATENCY     # 0.10

    @staticmethod
    def _clamp(val: float, min_val: float = 0.0, max_val: float = 1.0) -> float:
        """Clamps numerical inputs to valid [0.0, 1.0] range."""
        return max(min_val, min(max_val, float(val)))

    def calculate_priority(self, item: ConceptReviewItem) -> PrioritizedConcept:
        """
        Calculates priority score and factor breakdown for a single concept item.
        """
        recall = self._clamp(item.recall_score)
        time_elapsed = self._clamp(item.time_since_review)
        hint = self._clamp(item.hint_dependency)
        latency = self._clamp(item.response_latency)

        recall_factor = round(self.w_recall * (1.0 - recall), 4)
        time_factor = round(self.w_time * time_elapsed, 4)
        hint_factor = round(self.w_hint * hint, 4)
        latency_factor = round(self.w_latency * latency, 4)

        priority_score = round(recall_factor + time_factor + hint_factor + latency_factor, 4)
        # Ensure clamped between 0 and 1
        priority_score = min(1.0, max(0.0, priority_score))

        breakdown = PriorityBreakdown(
            recall_factor=recall_factor,
            time_factor=time_factor,
            hint_factor=hint_factor,
            latency_factor=latency_factor,
        )

        rationale = self._generate_rationale(
            concept_title=item.concept_title,
            priority=priority_score,
            recall=recall,
            time_elapsed=time_elapsed,
            hint=hint,
            latency=latency,
            breakdown=breakdown
        )

        return PrioritizedConcept(
            concept_id=item.concept_id,
            concept_title=item.concept_title,
            priority_score=priority_score,
            rank=0,  # Assigned after sorting
            breakdown=breakdown,
            rationale=rationale,
            original_data=item
        )

    def prioritize_queue(self, request: AdaptiveReviewRequest) -> AdaptiveReviewResponse:
        """
        Schedules concept review queue in descending order of priority score.
        Tie breaks deterministically by:
          1. Lower recall_score
          2. Higher time_since_review
          3. concept_id alphabetically
        """
        scored_items: List[PrioritizedConcept] = [
            self.calculate_priority(concept) for concept in request.concepts
        ]

        # Multi-key deterministic sort:
        # Descending priority (-score), ascending recall (+recall), descending time (-time), ascending ID
        scored_items.sort(
            key=lambda x: (
                -x.priority_score,
                x.original_data.recall_score,
                -x.original_data.time_since_review,
                x.concept_id
            )
        )

        # Assign 1-based ranks
        for index, item in enumerate(scored_items):
            item.rank = index + 1

        total_concepts = len(scored_items)

        # Apply top_k if requested
        if request.top_k is not None and request.top_k > 0:
            final_queue = scored_items[: request.top_k]
        else:
            final_queue = scored_items

        top_rec = None
        if final_queue:
            top = final_queue[0]
            top_rec = (
                f"Top review priority: '{top.concept_title}' (Score: {top.priority_score:.2f}). "
                f"Immediate review recommended to arrest memory decay."
            )

        return AdaptiveReviewResponse(
            queue=final_queue,
            total_concepts=total_concepts,
            top_recommendation=top_rec
        )

    @staticmethod
    def _generate_rationale(
        concept_title: str,
        priority: float,
        recall: float,
        time_elapsed: float,
        hint: float,
        latency: float,
        breakdown: PriorityBreakdown
    ) -> str:
        """Generates clear pedagogical explanation of why this concept received its priority."""
        if priority >= 0.70:
            urgency = "High Urgency"
            reason = (
                f"Critical decay risk. Low recall ({recall:.2f}) and high elapsed time ({time_elapsed:.2f}) "
                f"require immediate spatial reactivation."
            )
        elif priority >= 0.40:
            urgency = "Moderate Urgency"
            reason = (
                f"Partial retention. Moderate recall ({recall:.2f}) with hint dependency ({hint:.2f}) "
                f"or retrieval latency ({latency:.2f})."
            )
        else:
            urgency = "Low Urgency"
            reason = (
                f"Solid mastery. High recall score ({recall:.2f}) and recent review ({time_elapsed:.2f}) "
                f"indicate stable long-term retention."
            )

        formula_str = (
            f"0.45*(1-{recall:.2f})={breakdown.recall_factor:.3f} + "
            f"0.25*{time_elapsed:.2f}={breakdown.time_factor:.3f} + "
            f"0.20*{hint:.2f}={breakdown.hint_factor:.3f} + "
            f"0.10*{latency:.2f}={breakdown.latency_factor:.3f}"
        )

        return f"[{urgency}] {reason} ({formula_str})"


adaptive_service = AdaptiveSchedulerService()
