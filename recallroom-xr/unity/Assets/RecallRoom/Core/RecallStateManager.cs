using System;
using System.Collections.Generic;
using UnityEngine;
using RecallRoom.Data;

namespace RecallRoom.Core
{
    public enum RecallState
    {
        INITIALIZING = 0,
        HOME = 1,
        SELECTING_DECK = 2,
        PLACING_CARDS = 3,
        READY_TO_RECALL = 4,
        RECALLING = 5,
        EVALUATING = 6,
        SHOWING_FEEDBACK = 7,
        SESSION_COMPLETE = 8,
        ERROR = 9
    }

    [DisallowMultipleComponent]
    public class RecallStateManager : MonoBehaviour
    {
        public static RecallStateManager Instance { get; private set; }

        [Header("State Inspection")]
        [SerializeField] private RecallState currentState = RecallState.INITIALIZING;
        [SerializeField] private RecallState previousState = RecallState.INITIALIZING;
        [SerializeField] private string lastErrorMessage = string.Empty;

        [Header("Active Session Context")]
        [SerializeField] private StudyDeck activeDeck;
        [SerializeField] private int currentCardIndex = -1;
        [SerializeField] private SessionStats activeSessionStats;

        private readonly List<RecallState> stateHistory = new List<RecallState>();
        private readonly List<ConceptCardData> reviewQueue = new List<ConceptCardData>();

        // Events
        public event Action<RecallState, RecallState> OnStateChanged;
        public event Action<string> OnErrorMessage;
        public event Action<StudyDeck> OnDeckSelected;
        public event Action<ConceptCardData, int, int> OnActiveCardChanged;
        public event Action<CardReviewResult> OnCardEvaluated;
        public event Action<SessionStats> OnSessionCompleted;

        // Public Accessors
        public RecallState CurrentState => currentState;
        public RecallState PreviousState => previousState;
        public string LastErrorMessage => lastErrorMessage;
        public StudyDeck ActiveDeck => activeDeck;
        public int CurrentCardIndex => currentCardIndex;
        public SessionStats ActiveSessionStats => activeSessionStats;
        public IReadOnlyList<RecallState> StateHistory => stateHistory;
        public IReadOnlyList<ConceptCardData> ReviewQueue => reviewQueue;

        public ConceptCardData ActiveCard
        {
            get
            {
                if (reviewQueue != null && currentCardIndex >= 0 && currentCardIndex < reviewQueue.Count)
                {
                    return reviewQueue[currentCardIndex];
                }
                return null;
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            stateHistory.Add(currentState);
        }

        private void Start()
        {
            if (currentState == RecallState.INITIALIZING)
            {
                Initialize();
            }
        }

        public bool CanTransitionTo(RecallState targetState)
        {
            if (targetState == RecallState.ERROR) return true; // Error reachable from anywhere

            switch (currentState)
            {
                case RecallState.INITIALIZING:
                    return targetState == RecallState.HOME;

                case RecallState.HOME:
                    return targetState == RecallState.SELECTING_DECK || targetState == RecallState.INITIALIZING;

                case RecallState.SELECTING_DECK:
                    return targetState == RecallState.PLACING_CARDS || targetState == RecallState.HOME;

                case RecallState.PLACING_CARDS:
                    return targetState == RecallState.READY_TO_RECALL || targetState == RecallState.SELECTING_DECK || targetState == RecallState.HOME;

                case RecallState.READY_TO_RECALL:
                    return targetState == RecallState.RECALLING || targetState == RecallState.PLACING_CARDS || targetState == RecallState.HOME;

                case RecallState.RECALLING:
                    return targetState == RecallState.EVALUATING || targetState == RecallState.READY_TO_RECALL;

                case RecallState.EVALUATING:
                    return targetState == RecallState.SHOWING_FEEDBACK;

                case RecallState.SHOWING_FEEDBACK:
                    return targetState == RecallState.READY_TO_RECALL || targetState == RecallState.SESSION_COMPLETE;

                case RecallState.SESSION_COMPLETE:
                    return targetState == RecallState.HOME || targetState == RecallState.SELECTING_DECK;

                case RecallState.ERROR:
                    return targetState == RecallState.HOME || targetState == RecallState.INITIALIZING;

                default:
                    return false;
            }
        }

        public bool TransitionTo(RecallState newState)
        {
            if (!CanTransitionTo(newState))
            {
                Debug.LogWarning($"[RecallStateManager] Invalid transition attempted: {currentState} -> {newState}");
                return false;
            }

            previousState = currentState;
            currentState = newState;
            stateHistory.Add(newState);

            Debug.Log($"[RecallStateManager] State Transition: {previousState} -> {currentState}");
            OnStateChanged?.Invoke(previousState, currentState);
            return true;
        }

        public void Initialize(Action onComplete = null)
        {
            lastErrorMessage = string.Empty;
            currentCardIndex = -1;
            reviewQueue.Clear();

            if (currentState != RecallState.INITIALIZING)
            {
                TransitionTo(RecallState.INITIALIZING);
            }

            // Initialization logic (subsystems, audio, spatial anchors)
            Debug.Log("[RecallStateManager] Initialized subsystems successfully.");
            TransitionTo(RecallState.HOME);
            onComplete?.Invoke();
        }

        public void GoHome()
        {
            TransitionTo(RecallState.HOME);
        }

        public void OpenDeckSelection()
        {
            TransitionTo(RecallState.SELECTING_DECK);
        }

        public bool SelectDeck(StudyDeck deck)
        {
            if (deck == null || deck.Cards == null || deck.Cards.Count == 0)
            {
                TriggerError("Cannot select an empty or null study deck.");
                return false;
            }

            activeDeck = deck;
            reviewQueue.Clear();
            reviewQueue.AddRange(deck.Cards);
            currentCardIndex = 0;
            activeSessionStats = new SessionStats(deck.Id, deck.Title, deck.Cards.Count);

            OnDeckSelected?.Invoke(deck);
            return TransitionTo(RecallState.PLACING_CARDS);
        }

        public bool StartCardPlacement()
        {
            return TransitionTo(RecallState.PLACING_CARDS);
        }

        public bool ConfirmPlacements()
        {
            if (reviewQueue.Count == 0)
            {
                TriggerError("No cards available to recall in active deck.");
                return false;
            }

            currentCardIndex = 0;
            bool ok = TransitionTo(RecallState.READY_TO_RECALL);
            if (ok)
            {
                NotifyActiveCard();
            }
            return ok;
        }

        public bool BeginRecall()
        {
            if (ActiveCard == null)
            {
                TriggerError("Cannot begin recall: No active card selected.");
                return false;
            }

            return TransitionTo(RecallState.RECALLING);
        }

        public bool StartEvaluating()
        {
            return TransitionTo(RecallState.EVALUATING);
        }

        public bool DisplayFeedback(CardReviewResult result)
        {
            if (result == null)
            {
                TriggerError("Null evaluation result received.");
                return false;
            }

            if (activeSessionStats != null)
            {
                activeSessionStats.RecordResult(result);
            }

            OnCardEvaluated?.Invoke(result);
            return TransitionTo(RecallState.SHOWING_FEEDBACK);
        }

        public void NextCardOrComplete()
        {
            if (currentState != RecallState.SHOWING_FEEDBACK)
            {
                Debug.LogWarning($"[RecallStateManager] NextCardOrComplete called from invalid state {currentState}");
                return;
            }

            currentCardIndex++;
            if (currentCardIndex < reviewQueue.Count)
            {
                TransitionTo(RecallState.READY_TO_RECALL);
                NotifyActiveCard();
            }
            else
            {
                CompleteSession();
            }
        }

        public void CompleteSession()
        {
            if (activeSessionStats != null)
            {
                activeSessionStats.CompleteSession();
            }

            TransitionTo(RecallState.SESSION_COMPLETE);
            OnSessionCompleted?.Invoke(activeSessionStats);
        }

        public void SetReviewQueue(List<ConceptCardData> prioritizedCards)
        {
            if (prioritizedCards == null || prioritizedCards.Count == 0) return;
            reviewQueue.Clear();
            reviewQueue.AddRange(prioritizedCards);
            currentCardIndex = Mathf.Clamp(currentCardIndex, 0, reviewQueue.Count - 1);
            NotifyActiveCard();
        }

        public void TriggerError(string message)
        {
            lastErrorMessage = message;
            Debug.LogError($"[RecallStateManager] ERROR: {message}");
            OnErrorMessage?.Invoke(message);
            TransitionTo(RecallState.ERROR);
        }

        private void NotifyActiveCard()
        {
            if (ActiveCard != null)
            {
                OnActiveCardChanged?.Invoke(ActiveCard, currentCardIndex, reviewQueue.Count);
            }
        }
    }
}
