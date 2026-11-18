using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RecallRoom.Core;
using RecallRoom.Audio;
using RecallRoom.API;
using RecallRoom.XR;

namespace RecallRoom.UI
{
    /// <summary>
    /// Controls the Settings spatial panel covering microphone permissions,
    /// tracking recentering, accessibility options, backend connection status, and privacy details.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        public static SettingsView Instance { get; private set; }

        [Header("Root Panel")]
        [SerializeField] private GameObject settingsPanel;

        [Header("Microphone & Audio Controls")]
        [SerializeField] private TMP_Text micStatusText;
        [SerializeField] private Button micTestButton;
        [SerializeField] private Image micVolumeBar;

        [Header("Recenter & Seated Envelope")]
        [SerializeField] private Button recenterButton;
        [SerializeField] private TMP_Dropdown reachEnvelopeDropdown;

        [Header("Accessibility Settings")]
        [SerializeField] private Toggle nonSpeechModeToggle;
        [SerializeField] private Toggle highContrastToggle;
        [SerializeField] private Toggle largeTargetToggle;

        [Header("Backend Connection Status")]
        [SerializeField] private TMP_Text backendStatusText;
        [SerializeField] private TMP_InputField backendUrlInput;
        [SerializeField] private Button testConnectionButton;
        [SerializeField] private Toggle forceOfflineToggle;

        [Header("Privacy Information")]
        [SerializeField] private GameObject privacyModalPanel;
        [SerializeField] private Button openPrivacyButton;
        [SerializeField] private Button closePrivacyButton;

        [Header("Navigation")]
        [SerializeField] private Button closeSettingsButton;

        // Accessibility State Properties
        public bool IsNonSpeechMode { get; private set; } = false;
        public bool IsHighContrast { get; private set; } = false;
        public bool IsLargeTarget { get; private set; } = false;

        public event Action<bool> OnNonSpeechModeChanged;
        public event Action<bool> OnHighContrastChanged;

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

            WireButtons();
            Hide();
        }

        private void Start()
        {
            UpdateMicStatus();
            UpdateBackendStatus();
        }

        private void WireButtons()
        {
            if (recenterButton != null) recenterButton.onClick.AddListener(OnRecenterClicked);
            if (micTestButton != null) micTestButton.onClick.AddListener(OnTestMicClicked);
            if (testConnectionButton != null) testConnectionButton.onClick.AddListener(OnTestConnectionClicked);
            if (openPrivacyButton != null) openPrivacyButton.onClick.AddListener(() => ShowPrivacy(true));
            if (closePrivacyButton != null) closePrivacyButton.onClick.AddListener(() => ShowPrivacy(false));
            if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(Hide);

            if (nonSpeechModeToggle != null)
            {
                nonSpeechModeToggle.onValueChanged.AddListener((val) =>
                {
                    IsNonSpeechMode = val;
                    OnNonSpeechModeChanged?.Invoke(val);
                    Debug.Log($"[SettingsView] Non-Speech / Interactive Choice Mode: {val}");
                });
            }

            if (highContrastToggle != null)
            {
                highContrastToggle.onValueChanged.AddListener((val) =>
                {
                    IsHighContrast = val;
                    OnHighContrastChanged?.Invoke(val);
                });
            }

            if (forceOfflineToggle != null)
            {
                forceOfflineToggle.onValueChanged.AddListener((val) =>
                {
                    if (RecallRoomApiClient.Instance != null)
                    {
                        RecallRoomApiClient.Instance.ForceOfflineMode = val;
                    }
                    UpdateBackendStatus();
                });
            }
        }

        public void Show()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
            UpdateMicStatus();
            UpdateBackendStatus();
        }

        public void Hide()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            ShowPrivacy(false);
        }

        public void Toggle()
        {
            if (settingsPanel != null)
            {
                if (settingsPanel.activeSelf) Hide();
                else Show();
            }
        }

        private void OnRecenterClicked()
        {
            Debug.Log("[SettingsView] User triggered workspace recenter.");
            if (XRBootstrap.Instance != null)
            {
                XRBootstrap.Instance.RecenterTrackingOrigin();
            }
        }

        private void OnTestMicClicked()
        {
            if (MicCaptureController.Instance != null)
            {
                bool perm = MicCaptureController.Instance.HasMicrophonePermission;
                if (!perm)
                {
                    MicCaptureController.Instance.RequestMicrophonePermission();
                }
                UpdateMicStatus();
            }
        }

        private async void OnTestConnectionClicked()
        {
            if (backendStatusText != null) backendStatusText.text = "Checking connection...";
            if (RecallRoomApiClient.Instance != null)
            {
                if (backendUrlInput != null && !string.IsNullOrEmpty(backendUrlInput.text))
                {
                    RecallRoomApiClient.Instance.BaseUrl = backendUrlInput.text.Trim();
                }

                bool healthy = await RecallRoomApiClient.Instance.CheckHealthAsync();
                if (backendStatusText != null)
                {
                    backendStatusText.text = healthy ? "<color=#22c55e>Connected (FastAPI Live)</color>" : "<color=#ef4444>Offline (Using Local Deterministic Engine)</color>";
                }
            }
        }

        private void UpdateMicStatus()
        {
            if (micStatusText == null) return;
            bool hasPerm = MicCaptureController.Instance != null && MicCaptureController.Instance.HasMicrophonePermission;
            string dev = MicCaptureController.Instance != null ? MicCaptureController.Instance.SelectedDevice : "Default";
            micStatusText.text = hasPerm ? $"<color=#22c55e>Active</color> ({dev})" : "<color=#eab308>Permission Required</color>";
        }

        private void UpdateBackendStatus()
        {
            if (backendStatusText == null) return;
            bool offline = RecallRoomApiClient.Instance != null && RecallRoomApiClient.Instance.ForceOfflineMode;
            if (offline)
            {
                backendStatusText.text = "<color=#eab308>Forced Offline Mode</color>";
            }
            else
            {
                string url = RecallRoomApiClient.Instance != null ? RecallRoomApiClient.Instance.BaseUrl : "http://localhost:8000";
                backendStatusText.text = $"Target: {url}";
            }
        }

        public void ShowPrivacy(bool show)
        {
            if (privacyModalPanel != null) privacyModalPanel.SetActive(show);
        }
    }
}
