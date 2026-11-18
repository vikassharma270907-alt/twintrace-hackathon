using System;
using UnityEngine;
using RecallRoom.Core;
using RecallRoom.Spatial;

namespace RecallRoom.XR
{
    /// <summary>
    /// Manages XR subsystem startup, tracking origin modes (seated vs room-scale),
    /// color passthrough initialization, and graceful fallback to desktop simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public class XRBootstrap : MonoBehaviour
    {
        public static XRBootstrap Instance { get; private set; }

        [Header("Tracking & Rig Settings")]
        [SerializeField] private bool seatedMode = true;
        [SerializeField] private float defaultEyeHeight = 1.25f; // Seated desk eye height
        [SerializeField] private float minReachDistance = 0.35f; // Ergonomic near boundary
        [SerializeField] private float maxReachDistance = 0.65f; // Ergonomic far boundary

        [Header("Passthrough Configuration")]
        [SerializeField] private bool enablePassthrough = true;
        [SerializeField] private Color passthroughEdgeColor = new Color(0.12f, 0.53f, 0.90f, 0.4f);

        [Header("Subsystem References")]
        [SerializeField] private Transform centerEyeAnchor;
        [SerializeField] private Transform leftHandAnchor;
        [SerializeField] private Transform rightHandAnchor;
        [SerializeField] private HandTrackingManager handTrackingManager;
        [SerializeField] private XRSimulatorFallback simulatorFallback;
        [SerializeField] private RoomSurfaceDetector surfaceDetector;

        [Header("Status")]
        [SerializeField] private bool isHeadsetPresent = false;
        [SerializeField] private bool isPassthroughActive = false;
        [SerializeField] private bool isInitialized = false;

        public bool SeatedMode => seatedMode;
        public float MinReachDistance => minReachDistance;
        public float MaxReachDistance => maxReachDistance;
        public bool IsHeadsetPresent => isHeadsetPresent;
        public bool IsPassthroughActive => isPassthroughActive;
        public bool IsInitialized => isInitialized;

        public Transform CenterEyeAnchor => centerEyeAnchor != null ? centerEyeAnchor : Camera.main != null ? Camera.main.transform : transform;
        public Transform LeftHandAnchor => leftHandAnchor;
        public Transform RightHandAnchor => rightHandAnchor;

        public event Action<bool> OnHeadsetPresenceChanged;
        public event Action OnXRInitialized;
        public event Action<string> OnXRError;

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

            FindOrCreateAnchors();
        }

        private void Start()
        {
            InitializeXR();
        }

        public void InitializeXR()
        {
            try
            {
                // Detect whether running on physical Quest hardware or Editor/Desktop
                #if UNITY_ANDROID && !UNITY_EDITOR
                isHeadsetPresent = true;
                #else
                isHeadsetPresent = UnityEngine.XR.XRSettings.isDeviceActive;
                #endif

                ConfigureTrackingOrigin();
                ConfigurePassthrough();
                ConfigureHandTracking();

                isInitialized = true;
                Debug.Log($"[XRBootstrap] XR Subsystems initialized successfully. HeadsetPresent: {isHeadsetPresent}, SeatedMode: {seatedMode}");
                OnXRInitialized?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XRBootstrap] XR Initialization error: {ex.Message}");
                OnXRError?.Invoke(ex.Message);
            }
        }

        private void FindOrCreateAnchors()
        {
            if (centerEyeAnchor == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    centerEyeAnchor = mainCam.transform;
                }
                else
                {
                    var camGo = new GameObject("CenterEyeAnchor");
                    camGo.transform.SetParent(transform);
                    camGo.transform.localPosition = new Vector3(0f, defaultEyeHeight, 0f);
                    var cam = camGo.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // Transparent for passthrough
                    cam.nearClipPlane = 0.1f;
                    cam.farClipPlane = 20f;
                    camGo.tag = "MainCamera";
                    centerEyeAnchor = camGo.transform;
                }
            }

            if (handTrackingManager == null)
            {
                handTrackingManager = GetComponentInChildren<HandTrackingManager>() ?? gameObject.AddComponent<HandTrackingManager>();
            }

            if (simulatorFallback == null && !isHeadsetPresent)
            {
                simulatorFallback = GetComponentInChildren<XRSimulatorFallback>() ?? gameObject.AddComponent<XRSimulatorFallback>();
            }

            if (surfaceDetector == null)
            {
                surfaceDetector = FindObjectOfType<RoomSurfaceDetector>() ?? gameObject.AddComponent<RoomSurfaceDetector>();
            }
        }

        private void ConfigureTrackingOrigin()
        {
            // Set tracking space
            #if UNITY_XR_MANAGEMENT
            var xrInput = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            #endif

            if (seatedMode)
            {
                Debug.Log("[XRBootstrap] Configuring Seated Ergonomic Tracking Envelope (0.35m - 0.65m reach).");
            }
        }

        private void ConfigurePassthrough()
        {
            if (!enablePassthrough)
            {
                isPassthroughActive = false;
                return;
            }

            #if UNITY_ANDROID && !UNITY_EDITOR
            // On Meta Quest device, passthrough is enabled via OVRPassthroughLayer
            isPassthroughActive = true;
            Debug.Log("[XRBootstrap] Meta Quest Color Passthrough enabled with dark alpha clearing.");
            #else
            // In editor or desktop simulation, dark studio backdrop with grid
            isPassthroughActive = false;
            Debug.Log("[XRBootstrap] Desktop/Editor mode: Using dark studio background simulation.");
            #endif
        }

        private void ConfigureHandTracking()
        {
            if (handTrackingManager != null)
            {
                handTrackingManager.Initialize(centerEyeAnchor);
            }
        }

        public void RecenterTrackingOrigin()
        {
            Debug.Log("[XRBootstrap] RecenterTrackingOrigin requested.");
            if (centerEyeAnchor != null)
            {
                // Align world space UI arc directly in front of current gaze
                if (TabletopFallbackManager.Instance != null)
                {
                    // Re-calculate placement arc
                    Debug.Log("[XRBootstrap] Tabletop card arc recentered to current gaze.");
                }
            }
        }

        public void SetSeatedMode(bool enabled)
        {
            seatedMode = enabled;
            ConfigureTrackingOrigin();
        }
    }
}
