using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RecallRoom.Core;
using RecallRoom.Data;
using RecallRoom.Spatial;
using RecallRoom.Audio;
using RecallRoom.API;

namespace RecallRoom.UI
{
    public class WorldSpaceUIManager : MonoBehaviour
    {
        public static WorldSpaceUIManager Instance { get; private set; }

        [Header("Subsystem References")]
        [SerializeField] private HUDController hudController;
        [SerializeField] private SessionSummaryView sessionSummaryView;
        [SerializeField] private SettingsView settingsView;
        [SerializeField] private RoomSurfaceDetector surfaceDetector;
        [SerializeField] private MicCaptureController micController;
        [SerializeField] private RecallRoomApiClient apiClient;

        [Header("Menu Canvases")]
        [SerializeField] private GameObject homeMenuPanel;
        [SerializeField] private GameObject deckSelectionPanel;
        [SerializeField] private Transform deckButtonsContainer;
        [SerializeField] private Button deckButtonTemplate;

        [Header("Spatial Card Spawning")]
        [SerializeField] private GameObject spatialCardPrefab;
        [SerializeField] private Transform cardSpawnRoot;

        [Header("Built-in Decks")]
        [SerializeField] private TextAsset[] builtInDeckJsonFiles;

        private readonly List<StudyDeck> loadedDecks = new List<StudyDeck>();
        private readonly List<SpatialCardView> spawnedCardViews = new List<SpatialCardView>();
        private float recallStartTimestamp = 0f;
        private bool isEvaluating = false;

        public IReadOnlyList<StudyDeck> LoadedDecks => loadedDecks;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            LoadBuiltInDecks();
        }

        private void Start()
        {
            if (RecallStateManager.Instance != null)
            {
                RecallStateManager.Instance.OnStateChanged += OnFsmStateChanged;
            }

            PopulateDeckSelectionUI();
        }

        private void OnDestroy()
        {
            if (RecallStateManager.Instance != null)
            {
                RecallStateManager.Instance.OnStateChanged -= OnFsmStateChanged;
            }
        }

        private void LoadBuiltInDecks()
        {
            loadedDecks.Clear();
            if (builtInDeckJsonFiles != null && builtInDeckJsonFiles.Length > 0)
            {
                foreach (var asset in builtInDeckJsonFiles)
                {
                    if (asset != null && !string.IsNullOrEmpty(asset.text))
                    {
                        var deck = StudyDeck.FromJson(asset.text);
                        if (deck != null) loadedDecks.Add(deck);
                    }
                }
            }

            // Fallback: If TextAssets aren't wired in inspector, load from Resources or create default
            if (loadedDecks.Count == 0)
            {
                var fallbackDeck = CreateFallbackDeck();
                loadedDecks.Add(fallbackDeck);
            }
        }

        private void PopulateDeckSelectionUI()
        {
            if (deckButtonsContainer == null || deckButtonTemplate == null) return;

            deckButtonTemplate.gameObject.SetActive(false);

            foreach (var deck in loadedDecks)
            {
                var btn = Instantiate(deckButtonTemplate, deckButtonsContainer);
                btn.gameObject.SetActive(true);
                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = $"{deck.Title} ({deck.CardCount} cards)";
                }

                StudyDeck capturedDeck = deck;
                btn.onClick.AddListener(() =>
                {
                    OnDeckSelectedFromUI(capturedDeck);
                });
            }
        }

        public void OnDeckSelectedFromUI(StudyDeck deck)
        {
            if (deck == null || RecallStateManager.Instance == null) return;
            RecallStateManager.Instance.SelectDeck(deck);
        }

        private void OnFsmStateChanged(RecallState prevState, RecallState newState)
        {
            switch (newState)
            {
                case RecallState.HOME:
                    if (homeMenuPanel != null) homeMenuPanel.SetActive(true);
                    if (deckSelectionPanel != null) deckSelectionPanel.SetActive(false);
                    ClearSpawnedCards();
                    break;

                case RecallState.SELECTING_DECK:
                    if (homeMenuPanel != null) homeMenuPanel.SetActive(false);
                    if (deckSelectionPanel != null) deckSelectionPanel.SetActive(true);
                    break;

                case RecallState.PLACING_CARDS:
                    if (homeMenuPanel != null) homeMenuPanel.SetActive(false);
                    if (deckSelectionPanel != null) deckSelectionPanel.SetActive(false);
                    SpawnAndDistributeCards(RecallStateManager.Instance.ReviewQueue);
                    break;

                case RecallState.READY_TO_RECALL:
                    FocusActiveCard(RecallStateManager.Instance.ActiveCard);
                    break;

                case RecallState.RECALLING:
                    recallStartTimestamp = Time.time;
                    StartVoiceRecording();
                    break;

                case RecallState.SESSION_COMPLETE:
                    ClearSpawnedCards();
                    break;
            }
        }

        private void SpawnAndDistributeCards(IReadOnlyList<ConceptCardData> cards)
        {
            ClearSpawnedCards();
            if (cards == null || cards.Count == 0) return;

            Transform head = Camera.main != null ? Camera.main.transform : transform;
            var cardList = new List<ConceptCardData>(cards);
            var placements = surfaceDetector != null
                ? surfaceDetector.DistributeCardsOnSurfaces(cardList, head)
                : new List<ConceptPlacement>();

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                Vector3 spawnPos = i < placements.Count ? placements[i].Position : head.position + head.forward * 0.8f;
                Quaternion spawnRot = i < placements.Count ? placements[i].Rotation : Quaternion.identity;

                GameObject cardGo;
                if (spatialCardPrefab != null)
                {
                    cardGo = Instantiate(spatialCardPrefab, spawnPos, spawnRot, cardSpawnRoot);
                }
                else
                {
                    // Create default card instance if prefab is not assigned
                    cardGo = new GameObject($"SpatialCard_{card.Id}");
                    cardGo.transform.SetPositionAndRotation(spawnPos, spawnRot);
                    cardGo.AddComponent<SpatialCardAnchor>();
                    cardGo.AddComponent<SpatialCardView>();
                }

                var anchor = cardGo.GetComponent<SpatialCardAnchor>();
                if (anchor != null) anchor.Initialize(card.Id, spawnPos, spawnRot);

                var view = cardGo.GetComponent<SpatialCardView>();
                if (view != null)
                {
                    view.Bind(card);
                    spawnedCardViews.Add(view);
                }
            }
        }

        private void FocusActiveCard(ConceptCardData activeCard)
        {
            if (activeCard == null) return;
            bool isNonSpeech = settingsView != null && settingsView.IsNonSpeechMode;

            foreach (var view in spawnedCardViews)
            {
                if (view.BoundCard != null)
                {
                    bool isActive = (view.BoundCard.Id == activeCard.Id);
                    var anchor = view.GetComponent<SpatialCardAnchor>();
                    if (anchor != null)
                    {
                        anchor.SetState(isActive ? CardAnchorState.ActiveRecall : CardAnchorState.Placed);
                    }

                    if (isActive)
                    {
                        string[] choiceOptions = null;
                        if (isNonSpeech)
                        {
                            choiceOptions = GenerateChoiceOptions(activeCard);
                        }
                        view.ShowRecallPrompt(hideAnswers: true, showChoices: isNonSpeech, choiceOptions: choiceOptions, onChoiceSelected: SubmitActiveRecallChoice);
                    }
                    else
                    {
                        view.ShowRecallPrompt(hideAnswers: true, showChoices: false);
                    }
                }
            }
        }

        private string[] GenerateChoiceOptions(ConceptCardData activeCard)
        {
            var options = new List<string>();
            string correctAnswer = (activeCard.AcceptedAnswers != null && activeCard.AcceptedAnswers.Length > 0)
                ? activeCard.AcceptedAnswers[0]
                : activeCard.Summary;
            options.Add(correctAnswer);

            // Add 2 distractors from other cards in the deck
            if (RecallStateManager.Instance != null && RecallStateManager.Instance.ActiveDeck != null)
            {
                foreach (var other in RecallStateManager.Instance.ActiveDeck.Cards)
                {
                    if (other.Id != activeCard.Id && options.Count < 3)
                    {
                        string distractor = (other.AcceptedAnswers != null && other.AcceptedAnswers.Length > 0)
                            ? other.AcceptedAnswers[0]
                            : other.Summary;
                        if (!options.Contains(distractor)) options.Add(distractor);
                    }
                }
            }

            // Shuffle options
            for (int i = 0; i < options.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, options.Count);
                string tmp = options[i];
                options[i] = options[r];
                options[r] = tmp;
            }

            return options.ToArray();
        }

        public async void SubmitActiveRecallChoice(string chosenText)
        {
            if (isEvaluating || RecallStateManager.Instance == null) return;
            isEvaluating = true;

            float latency = Time.time - recallStartTimestamp;
            RecallStateManager.Instance.StartEvaluating();

            var activeCard = RecallStateManager.Instance.ActiveCard;
            bool hintUsed = false;
            SpatialCardView activeView = null;

            foreach (var view in spawnedCardViews)
            {
                if (view.BoundCard != null && view.BoundCard.Id == activeCard.Id)
                {
                    activeView = view;
                    hintUsed = view.IsHintRevealed;
                    break;
                }
            }

            EvaluateResponse evalResponse;
            if (apiClient != null && !apiClient.ForceOfflineMode)
            {
                evalResponse = await apiClient.EvaluateRecallAsync(activeCard, chosenText, latency, hintUsed);
            }
            else
            {
                evalResponse = RecallRoomApiClient.EvaluateOfflineDeterministic(activeCard, chosenText, latency, hintUsed);
            }

            if (activeView != null)
            {
                activeView.DisplayFeedback(evalResponse, chosenText);
            }

            ReviewStatus status = ReviewStatus.RETRY;
            if (evalResponse.status == "CORRECT") status = ReviewStatus.CORRECT;
            else if (evalResponse.status == "PARTIAL") status = ReviewStatus.PARTIAL;

            var result = new CardReviewResult(
                activeCard.Id,
                evalResponse.score,
                status,
                latency,
                hintUsed,
                chosenText,
                evalResponse.feedback
            );

            RecallStateManager.Instance.DisplayFeedback(result);
            isEvaluating = false;
        }

        public void OpenSettings()
        {
            if (settingsView != null)
            {
                settingsView.Show();
            }
        }

        public async void SubmitActiveRecallVoice()
        {
            if (isEvaluating || micController == null || RecallStateManager.Instance == null) return;
            isEvaluating = true;

            float latency = Time.time - recallStartTimestamp;
            byte[] wavBytes = micController.StopRecording();

            RecallStateManager.Instance.StartEvaluating();

            // 1. Transcribe
            string transcript = string.Empty;
            if (apiClient != null)
            {
                transcript = await apiClient.TranscribeAudioAsync(wavBytes);
            }

            // 2. Evaluate
            var activeCard = RecallStateManager.Instance.ActiveCard;
            bool hintUsed = false;
            SpatialCardView activeView = null;

            foreach (var view in spawnedCardViews)
            {
                if (view.BoundCard != null && view.BoundCard.Id == activeCard.Id)
                {
                    activeView = view;
                    hintUsed = view.IsHintRevealed;
                    break;
                }
            }

            EvaluateResponse evalResponse;
            if (apiClient != null)
            {
                evalResponse = await apiClient.EvaluateRecallAsync(activeCard, transcript, latency, hintUsed);
            }
            else
            {
                evalResponse = RecallRoomApiClient.EvaluateOfflineDeterministic(activeCard, transcript, latency, hintUsed);
            }

            // 3. Update view feedback
            if (activeView != null)
            {
                activeView.DisplayFeedback(evalResponse, transcript);
            }

            ReviewStatus status = ReviewStatus.RETRY;
            if (evalResponse.status == "CORRECT") status = ReviewStatus.CORRECT;
            else if (evalResponse.status == "PARTIAL") status = ReviewStatus.PARTIAL;

            var result = new CardReviewResult(
                activeCard.Id,
                evalResponse.score,
                status,
                latency,
                hintUsed,
                transcript,
                evalResponse.feedback
            );

            RecallStateManager.Instance.DisplayFeedback(result);
            isEvaluating = false;
        }

        private void StartVoiceRecording()
        {
            if (micController != null)
            {
                micController.StartRecording();
            }
        }

        private void ClearSpawnedCards()
        {
            foreach (var view in spawnedCardViews)
            {
                if (view != null && view.gameObject != null)
                {
                    Destroy(view.gameObject);
                }
            }
            spawnedCardViews.Clear();
        }

        private StudyDeck CreateFallbackDeck()
        {
            var cards = new List<ConceptCardData>
            {
                new ConceptCardData("sample_1", "fallback_deck", "Spaced Repetition", "Reviewing at expanding intervals.", "Leitner Box", new string[]{"intervals", "retention"}, new string[]{"Reviewing at expanding intervals"}, 1, "Learning", "Think of expanding time intervals.")
            };
            return new StudyDeck("fallback_deck", "Quick Sample Deck", "Sample deck for testing.", "Sample", cards);
        }
    }
}
