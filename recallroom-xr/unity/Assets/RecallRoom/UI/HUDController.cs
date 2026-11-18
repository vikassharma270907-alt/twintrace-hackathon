using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RecallRoom.Core;
using RecallRoom.Audio;
using RecallRoom.Data;

namespace RecallRoom.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Head-Follow Movement")]
        [SerializeField] private Transform userHeadTransform;
        [SerializeField] private float distanceInFront = 1.2f;
        [SerializeField] private float heightOffset = -0.1f;
        [SerializeField] private float followPositionSpeed = 4f;
        [SerializeField] private float followRotationSpeed = 4f;

        [Header("UI Text Displays")]
        [SerializeField] private TMP_Text stateBadgeText;
        [SerializeField] private TMP_Text deckTitleText;
        [SerializeField] private TMP_Text cardProgressText;
        [SerializeField] private TMP_Text timerText;

        [Header("Microphone & Audio Meter")]
        [SerializeField] private GameObject micIndicatorRoot;
        [SerializeField] private Image micRmsBar;
        [SerializeField] private Color micIdleColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color micRecordingColor = new Color(0.9f, 0.2f, 0.2f);

        private float sessionTimer = 0f;
        private bool isTimerRunning = false;

        private void Start()
        {
            if (userHeadTransform == null && Camera.main != null)
            {
                userHeadTransform = Camera.main.transform;
            }

            // Hook state manager events
            if (RecallStateManager.Instance != null)
            {
                RecallStateManager.Instance.OnStateChanged += HandleStateChanged;
                RecallStateManager.Instance.OnDeckSelected += HandleDeckSelected;
                RecallStateManager.Instance.OnActiveCardChanged += HandleActiveCardChanged;
            }

            // Hook mic events
            if (MicCaptureController.Instance != null)
            {
                MicCaptureController.Instance.OnVoiceLevelChanged += HandleVoiceLevelChanged;
                MicCaptureController.Instance.OnRecordingStarted += HandleRecordingStarted;
                MicCaptureController.Instance.OnRecordingStopped += HandleRecordingStopped;
            }

            UpdateStateBadge(RecallStateManager.Instance != null ? RecallStateManager.Instance.CurrentState : RecallState.INITIALIZING);
            SetMicActive(false);
        }

        private void OnDestroy()
        {
            if (RecallStateManager.Instance != null)
            {
                RecallStateManager.Instance.OnStateChanged -= HandleStateChanged;
                RecallStateManager.Instance.OnDeckSelected -= HandleDeckSelected;
                RecallStateManager.Instance.OnActiveCardChanged -= HandleActiveCardChanged;
            }

            if (MicCaptureController.Instance != null)
            {
                MicCaptureController.Instance.OnVoiceLevelChanged -= HandleVoiceLevelChanged;
                MicCaptureController.Instance.OnRecordingStarted -= HandleRecordingStarted;
                MicCaptureController.Instance.OnRecordingStopped -= HandleRecordingStopped;
            }
        }

        private void Update()
        {
            FollowUserHead();

            if (isTimerRunning)
            {
                sessionTimer += Time.deltaTime;
                UpdateTimerText();
            }
        }

        private void FollowUserHead()
        {
            if (userHeadTransform == null) return;

            Vector3 headPos = userHeadTransform.position;
            Vector3 headForward = userHeadTransform.forward;
            headForward.y = 0f;
            if (headForward.sqrMagnitude < 0.001f) headForward = Vector3.forward;
            headForward.Normalize();

            Vector3 targetPos = headPos + (headForward * distanceInFront) + (Vector3.up * heightOffset);
            Quaternion targetRot = Quaternion.LookRotation(targetPos - headPos, Vector3.up);

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followPositionSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * followRotationSpeed);
        }

        private void HandleStateChanged(RecallState prevState, RecallState newState)
        {
            UpdateStateBadge(newState);

            if (newState == RecallState.READY_TO_RECALL && !isTimerRunning)
            {
                sessionTimer = 0f;
                isTimerRunning = true;
            }
            else if (newState == RecallState.SESSION_COMPLETE || newState == RecallState.HOME)
            {
                isTimerRunning = false;
            }
        }

        private void HandleDeckSelected(StudyDeck deck)
        {
            if (deckTitleText != null && deck != null)
            {
                deckTitleText.text = deck.Title;
            }
        }

        private void HandleActiveCardChanged(ConceptCardData card, int index, int total)
        {
            if (cardProgressText != null)
            {
                cardProgressText.text = $"Card {index + 1} / {total}";
            }
        }

        private void HandleRecordingStarted()
        {
            SetMicActive(true);
        }

        private void HandleRecordingStopped(byte[] wavBytes, float duration)
        {
            SetMicActive(false);
        }

        private void HandleVoiceLevelChanged(float rms)
        {
            if (micRmsBar != null)
            {
                micRmsBar.fillAmount = Mathf.Clamp01(rms * 10f); // amplified for visual feedback
            }
        }

        private void SetMicActive(bool active)
        {
            if (micIndicatorRoot != null)
            {
                micIndicatorRoot.SetActive(active);
            }
            if (micRmsBar != null)
            {
                micRmsBar.color = active ? micRecordingColor : micIdleColor;
                if (!active) micRmsBar.fillAmount = 0f;
            }
        }

        private void UpdateStateBadge(RecallState state)
        {
            if (stateBadgeText == null) return;
            stateBadgeText.text = state.ToString().Replace('_', ' ');
        }

        private void UpdateTimerText()
        {
            if (timerText == null) return;
            int minutes = Mathf.FloorToInt(sessionTimer / 60f);
            int seconds = Mathf.FloorToInt(sessionTimer % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}
