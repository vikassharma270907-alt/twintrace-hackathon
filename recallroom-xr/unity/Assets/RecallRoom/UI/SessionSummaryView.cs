using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RecallRoom.Core;
using RecallRoom.Data;
using RecallRoom.Learning;

namespace RecallRoom.UI
{
    public class SessionSummaryView : MonoBehaviour
    {
        [Header("Root Object")]
        [SerializeField] private GameObject summaryPanel;

        [Header("Statistics Displays")]
        [SerializeField] private TMP_Text deckTitleText;
        [SerializeField] private TMP_Text accuracyPercentText;
        [SerializeField] private TMP_Text correctCountText;
        [SerializeField] private TMP_Text partialCountText;
        [SerializeField] private TMP_Text retryCountText;
        [SerializeField] private TMP_Text avgLatencyText;
        [SerializeField] private TMP_Text sessionDurationText;

        [Header("Weakest Concepts Recommendation")]
        [SerializeField] private TMP_Text recommendationsListText;

        [Header("Interactive Action Buttons")]
        [SerializeField] private Button reviewWeakestButton;
        [SerializeField] private Button restartDeckButton;
        [SerializeField] private Button homeButton;

        private SessionStats currentStats;

        private void Awake()
        {
            if (reviewWeakestButton != null) reviewWeakestButton.onClick.AddListener(OnReviewWeakestClicked);
            if (restartDeckButton != null) restartDeckButton.onClick.AddListener(OnRestartDeckClicked);
            if (homeButton != null) homeButton.onClick.AddListener(OnHomeClicked);

            Hide();
        }

        private void Start()
        {
            if (RecallStateManager.Instance != null)
            {
                RecallStateManager.Instance.OnSessionCompleted += DisplaySummary;
            }
        }

        private void OnDestroy()
        {
            if (RecallStateManager.Instance != null)
            {
                RecallStateManager.Instance.OnSessionCompleted -= DisplaySummary;
            }
        }

        public void DisplaySummary(SessionStats stats)
        {
            currentStats = stats;
            if (stats == null) return;

            if (summaryPanel != null) summaryPanel.SetActive(true);

            if (deckTitleText != null) deckTitleText.text = stats.DeckTitle;
            if (accuracyPercentText != null) accuracyPercentText.text = $"{stats.GetAccuracyPercent():F1}%";
            if (correctCountText != null) correctCountText.text = $"{stats.CorrectCount} Correct";
            if (partialCountText != null) partialCountText.text = $"{stats.PartialCount} Partial";
            if (retryCountText != null) retryCountText.text = $"{stats.RetryCount} Retry";
            if (avgLatencyText != null) avgLatencyText.text = $"{stats.AverageLatencySeconds:F1}s avg latency";

            int minutes = Mathf.FloorToInt(stats.TotalDurationSeconds / 60f);
            int seconds = Mathf.FloorToInt(stats.TotalDurationSeconds % 60f);
            if (sessionDurationText != null) sessionDurationText.text = $"{minutes:00}:{seconds:00}";

            // Identify weakest concepts
            BuildRecommendations(stats);
        }

        public void Hide()
        {
            if (summaryPanel != null) summaryPanel.SetActive(false);
        }

        private void BuildRecommendations(SessionStats stats)
        {
            if (recommendationsListText == null) return;

            var weakCards = new List<string>();
            if (stats.CardResults != null)
            {
                foreach (var r in stats.CardResults)
                {
                    if (r.Status == ReviewStatus.RETRY || r.Status == ReviewStatus.PARTIAL)
                    {
                        weakCards.Add($"• <b>{r.CardId}</b>: {r.Status} ({(r.Score * 100f):F0}%)");
                    }
                }
            }

            if (weakCards.Count > 0)
            {
                recommendationsListText.text = string.Join("\n", weakCards);
                if (reviewWeakestButton != null) reviewWeakestButton.interactable = true;
            }
            else
            {
                recommendationsListText.text = "🎉 Flawless mastery! All concepts recalled accurately.";
                if (reviewWeakestButton != null) reviewWeakestButton.interactable = false;
            }
        }

        private void OnReviewWeakestClicked()
        {
            if (RecallStateManager.Instance == null || currentStats == null) return;

            var activeDeck = RecallStateManager.Instance.ActiveDeck;
            if (activeDeck == null) return;

            // Use AdaptiveScheduler to order cards
            var scheduled = AdaptiveScheduler.ScheduleDeck(activeDeck.Cards, currentStats.CardResults);
            var prioritizedCards = new List<ConceptCardData>();
            foreach (var entry in scheduled)
            {
                prioritizedCards.Add(entry.Card);
            }

            Hide();
            RecallStateManager.Instance.SetReviewQueue(prioritizedCards);
            RecallStateManager.Instance.StartCardPlacement();
        }

        private void OnRestartDeckClicked()
        {
            if (RecallStateManager.Instance == null) return;
            var activeDeck = RecallStateManager.Instance.ActiveDeck;
            Hide();
            if (activeDeck != null)
            {
                RecallStateManager.Instance.SelectDeck(activeDeck);
            }
            else
            {
                RecallStateManager.Instance.OpenDeckSelection();
            }
        }

        private void OnHomeClicked()
        {
            Hide();
            if (RecallStateManager.Instance != null)
            {
                RecallStateManager.Instance.GoHome();
            }
        }
    }
}
