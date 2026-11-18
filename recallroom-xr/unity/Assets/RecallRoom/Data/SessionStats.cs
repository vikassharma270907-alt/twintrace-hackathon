using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecallRoom.Data
{
    public enum ReviewStatus
    {
        UNREVIEWED = 0,
        CORRECT = 1,
        PARTIAL = 2,
        RETRY = 3
    }

    [Serializable]
    public class CardReviewResult
    {
        [SerializeField] private string cardId;
        [SerializeField] private float score;
        [SerializeField] private ReviewStatus status;
        [SerializeField] private float latencySeconds;
        [SerializeField] private bool hintUsed;
        [SerializeField] private float timeSinceReviewHours;
        [SerializeField] private string transcript;
        [SerializeField] private string feedback;
        [SerializeField] private long timestampTicks;

        public string CardId { get => cardId; set => cardId = value; }
        public float Score { get => Mathf.Clamp01(score); set => score = Mathf.Clamp01(value); }
        public ReviewStatus Status { get => status; set => status = value; }
        public float LatencySeconds { get => Mathf.Max(0f, latencySeconds); set => latencySeconds = Mathf.Max(0f, value); }
        public bool HintUsed { get => hintUsed; set => hintUsed = value; }
        public float TimeSinceReviewHours { get => Mathf.Max(0f, timeSinceReviewHours); set => timeSinceReviewHours = Mathf.Max(0f, value); }
        public string Transcript { get => transcript; set => transcript = value; }
        public string Feedback { get => feedback; set => feedback = value; }
        public DateTime Timestamp => new DateTime(timestampTicks > 0 ? timestampTicks : DateTime.UtcNow.Ticks, DateTimeKind.Utc);

        public CardReviewResult()
        {
            timestampTicks = DateTime.UtcNow.Ticks;
        }

        public CardReviewResult(string cardId, float score, ReviewStatus status, float latencySeconds, bool hintUsed, string transcript = "", string feedback = "", float timeSinceReviewHours = 0f)
        {
            this.cardId = cardId;
            this.score = Mathf.Clamp01(score);
            this.status = status;
            this.latencySeconds = Mathf.Max(0f, latencySeconds);
            this.hintUsed = hintUsed;
            this.transcript = transcript;
            this.feedback = feedback;
            this.timeSinceReviewHours = Mathf.Max(0f, timeSinceReviewHours);
            this.timestampTicks = DateTime.UtcNow.Ticks;
        }
    }

    [Serializable]
    public class SessionStats
    {
        [SerializeField] private string deckId;
        [SerializeField] private string deckTitle;
        [SerializeField] private long startTimeTicks;
        [SerializeField] private long endTimeTicks;
        [SerializeField] private int totalCards;
        [SerializeField] private int reviewedCards;
        [SerializeField] private int correctCount;
        [SerializeField] private int partialCount;
        [SerializeField] private int retryCount;
        [SerializeField] private float averageLatencySeconds;
        [SerializeField] private List<CardReviewResult> cardResults = new List<CardReviewResult>();

        public string DeckId { get => deckId; set => deckId = value; }
        public string DeckTitle { get => deckTitle; set => deckTitle = value; }
        public DateTime StartTime => new DateTime(startTimeTicks > 0 ? startTimeTicks : DateTime.UtcNow.Ticks, DateTimeKind.Utc);
        public DateTime EndTime => new DateTime(endTimeTicks > 0 ? endTimeTicks : DateTime.UtcNow.Ticks, DateTimeKind.Utc);
        public int TotalCards { get => totalCards; set => totalCards = value; }
        public int ReviewedCards => cardResults != null ? cardResults.Count : reviewedCards;
        public int CorrectCount => correctCount;
        public int PartialCount => partialCount;
        public int RetryCount => retryCount;
        public float AverageLatencySeconds => averageLatencySeconds;
        public List<CardReviewResult> CardResults { get => cardResults ?? (cardResults = new List<CardReviewResult>()); set => cardResults = value; }

        public float TotalDurationSeconds
        {
            get
            {
                if (startTimeTicks == 0) return 0f;
                long end = endTimeTicks > 0 ? endTimeTicks : DateTime.UtcNow.Ticks;
                return (float)TimeSpan.FromTicks(end - startTimeTicks).TotalSeconds;
            }
        }

        public SessionStats()
        {
            startTimeTicks = DateTime.UtcNow.Ticks;
        }

        public SessionStats(string deckId, string deckTitle, int totalCards)
        {
            this.deckId = deckId;
            this.deckTitle = deckTitle;
            this.totalCards = totalCards;
            this.startTimeTicks = DateTime.UtcNow.Ticks;
            this.cardResults = new List<CardReviewResult>();
        }

        public void RecordResult(CardReviewResult result)
        {
            if (result == null) return;
            if (cardResults == null) cardResults = new List<CardReviewResult>();

            cardResults.Add(result);
            reviewedCards = cardResults.Count;

            switch (result.Status)
            {
                case ReviewStatus.CORRECT:
                    correctCount++;
                    break;
                case ReviewStatus.PARTIAL:
                    partialCount++;
                    break;
                case ReviewStatus.RETRY:
                    retryCount++;
                    break;
            }

            // Recalculate average latency
            float totalLatency = 0f;
            foreach (var r in cardResults)
            {
                totalLatency += r.LatencySeconds;
            }
            averageLatencySeconds = cardResults.Count > 0 ? totalLatency / cardResults.Count : 0f;
        }

        public void CompleteSession()
        {
            endTimeTicks = DateTime.UtcNow.Ticks;
        }

        public float GetAccuracyPercent()
        {
            if (cardResults == null || cardResults.Count == 0) return 0f;
            // Full point for correct, 0.5 for partial
            float totalScore = (correctCount * 1.0f) + (partialCount * 0.5f);
            return (totalScore / cardResults.Count) * 100f;
        }
    }
}
