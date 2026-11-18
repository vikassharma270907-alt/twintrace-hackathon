using System;
using System.Collections.Generic;
using UnityEngine;
using RecallRoom.Data;

namespace RecallRoom.Learning
{
    public class PriorityEntry : IComparable<PriorityEntry>
    {
        public ConceptCardData Card { get; set; }
        public float Priority { get; set; }
        public float RecallScore { get; set; }
        public float NormalizedTime { get; set; }
        public float HintDependency { get; set; }
        public float NormalizedLatency { get; set; }
        public string Rationale { get; set; }

        public int CompareTo(PriorityEntry other)
        {
            if (other == null) return 1;
            // Higher priority comes first
            return other.Priority.CompareTo(this.Priority);
        }
    }

    public static class AdaptiveScheduler
    {
        public const float WEIGHT_RECALL = 0.45f;
        public const float WEIGHT_TIME = 0.25f;
        public const float WEIGHT_HINT = 0.20f;
        public const float WEIGHT_LATENCY = 0.10f;

        public const float DEFAULT_MAX_TIME_HOURS = 24.0f;
        public const float DEFAULT_MAX_LATENCY_SECONDS = 20.0f;

        public static float CalculatePriority(float recallScore, float timeSinceReviewHours, float hintDependency, float latencySeconds,
            float maxTimeHours = DEFAULT_MAX_TIME_HOURS, float maxLatencySeconds = DEFAULT_MAX_LATENCY_SECONDS)
        {
            float normRecall = Mathf.Clamp01(recallScore);
            float normTime = Mathf.Clamp01(timeSinceReviewHours / Mathf.Max(0.001f, maxTimeHours));
            float normHint = Mathf.Clamp01(hintDependency);
            float normLatency = Mathf.Clamp01(latencySeconds / Mathf.Max(0.001f, maxLatencySeconds));

            float termRecall = WEIGHT_RECALL * (1.0f - normRecall);
            float termTime = WEIGHT_TIME * normTime;
            float termHint = WEIGHT_HINT * normHint;
            float termLatency = WEIGHT_LATENCY * normLatency;

            float priority = termRecall + termTime + termHint + termLatency;
            return Mathf.Clamp01(priority);
        }

        public static List<PriorityEntry> ScheduleDeck(List<ConceptCardData> cards, List<CardReviewResult> history,
            float maxTimeHours = DEFAULT_MAX_TIME_HOURS, float maxLatencySeconds = DEFAULT_MAX_LATENCY_SECONDS)
        {
            var result = new List<PriorityEntry>();
            if (cards == null || cards.Count == 0) return result;

            // Map latest review per card
            var latestReviewMap = new Dictionary<string, CardReviewResult>();
            if (history != null)
            {
                foreach (var review in history)
                {
                    if (review != null && !string.IsNullOrEmpty(review.CardId))
                    {
                        latestReviewMap[review.CardId] = review;
                    }
                }
            }

            foreach (var card in cards)
            {
                float recall = 0.0f;
                float timeHours = maxTimeHours; // default to maximum unreviewed time if never reviewed
                float hint = 0.0f;
                float latency = 0.0f;

                if (latestReviewMap.TryGetValue(card.Id, out CardReviewResult rev))
                {
                    recall = rev.Score;
                    timeHours = rev.TimeSinceReviewHours > 0 ? rev.TimeSinceReviewHours : 1.0f;
                    hint = rev.HintUsed ? 1.0f : 0.0f;
                    latency = rev.LatencySeconds;
                }

                float priority = CalculatePriority(recall, timeHours, hint, latency, maxTimeHours, maxLatencySeconds);
                string rationale = GenerateRationale(recall, timeHours, hint, latency, rev != null);

                result.Add(new PriorityEntry
                {
                    Card = card,
                    Priority = priority,
                    RecallScore = recall,
                    NormalizedTime = Mathf.Clamp01(timeHours / maxTimeHours),
                    HintDependency = hint,
                    NormalizedLatency = Mathf.Clamp01(latency / maxLatencySeconds),
                    Rationale = rationale
                });
            }

            // Sort descending: highest priority first
            result.Sort();
            return result;
        }

        private static string GenerateRationale(float recall, float timeHours, float hint, float latency, bool hasReviewed)
        {
            if (!hasReviewed)
            {
                return "Unreviewed concept: scheduled for baseline recall assessment.";
            }

            var tags = new List<string>();
            if (recall < 0.5f) tags.Add($"Low recall ({(recall * 100f):F0}%)");
            else if (recall < 0.75f) tags.Add($"Partial recall ({(recall * 100f):F0}%)");

            if (hint > 0.5f) tags.Add("Hint dependency detected");
            if (timeHours >= 12f) tags.Add($"Long interval ({timeHours:F1}h)");
            if (latency >= 10f) tags.Add($"High latency ({latency:F1}s)");

            if (tags.Count == 0)
            {
                return $"Solid recall ({(recall * 100f):F0}%); scheduled for maintenance retention.";
            }

            return string.Join("; ", tags);
        }
    }
}
