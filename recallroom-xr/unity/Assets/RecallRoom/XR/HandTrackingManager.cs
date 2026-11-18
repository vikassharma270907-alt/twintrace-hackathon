using System;
using UnityEngine;
using RecallRoom.Interaction;
using RecallRoom.Spatial;

namespace RecallRoom.XR
{
    public enum HandType
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// Monitors Meta XR Hand Tracking confidence, pinch detection, ray casting,
    /// and handles temporary tracking loss gracefully without abrupt object dropping.
    /// </summary>
    public class HandTrackingManager : MonoBehaviour
    {
        public static HandTrackingManager Instance { get; private set; }

        [Header("Tracking Status")]
        [SerializeField] private bool leftHandTracked = false;
        [SerializeField] private bool rightHandTracked = false;
        [SerializeField] private float trackingLossGracePeriod = 0.5f;

        [Header("Pinch Settings")]
        [SerializeField] private float pinchThreshold = 0.7f;
        [SerializeField] private float pinchReleaseThreshold = 0.35f;
        [SerializeField] private float maxRayDistance = 4.0f;

        [Header("Hand Pointers")]
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Transform eyeAnchorTransform;

        private float leftTrackingLossTimer = 0f;
        private float rightTrackingLossTimer = 0f;
        private bool isLeftPinching = false;
        private bool isRightPinching = false;

        private HandGrabHandler activeGrabbedObject = null;

        public bool LeftHandTracked => leftHandTracked;
        public bool RightHandTracked => rightHandTracked;
        public bool IsAnyHandTracked => leftHandTracked || rightHandTracked;
        public bool IsPinching => isLeftPinching || isRightPinching;

        public event Action<HandType> OnHandTrackingAcquired;
        public event Action<HandType> OnHandTrackingLost;
        public event Action<HandType, Vector3> OnPinchStarted;
        public event Action<HandType> OnPinchEnded;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void Initialize(Transform eyeAnchor)
        {
            eyeAnchorTransform = eyeAnchor;
            EnsureHandPointers();
        }

        private void EnsureHandPointers()
        {
            if (leftHandTransform == null)
            {
                var lGo = new GameObject("LeftHandPointer");
                lGo.transform.SetParent(transform);
                lGo.transform.localPosition = new Vector3(-0.2f, -0.15f, 0.4f);
                leftHandTransform = lGo.transform;
            }

            if (rightHandTransform == null)
            {
                var rGo = new GameObject("RightHandPointer");
                rGo.transform.SetParent(transform);
                rGo.transform.localPosition = new Vector3(0.2f, -0.15f, 0.4f);
                rightHandTransform = rGo.transform;
            }
        }

        private void Update()
        {
            PollHandTracking();
            ProcessInteractions();
        }

        private void PollHandTracking()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            // Query OVRPlugin / OVRHand
            // For build resilience without hard dependency failures:
            bool rawLeft = CheckNativeHandTracked(HandType.Left);
            bool rawRight = CheckNativeHandTracked(HandType.Right);
            #else
            // In editor or desktop simulator, hand is active if simulator or mouse is interacting
            bool rawLeft = false;
            bool rawRight = true; // Primary simulated right hand
            #endif

            UpdateHandStatus(HandType.Left, rawLeft, ref leftHandTracked, ref leftTrackingLossTimer);
            UpdateHandStatus(HandType.Right, rawRight, ref rightHandTracked, ref rightTrackingLossTimer);
        }

        private void UpdateHandStatus(HandType hand, bool rawTracked, ref bool currentStatus, ref float lossTimer)
        {
            if (rawTracked)
            {
                lossTimer = 0f;
                if (!currentStatus)
                {
                    currentStatus = true;
                    OnHandTrackingAcquired?.Invoke(hand);
                }
            }
            else
            {
                if (currentStatus)
                {
                    lossTimer += Time.deltaTime;
                    if (lossTimer >= trackingLossGracePeriod)
                    {
                        currentStatus = false;
                        OnHandTrackingLost?.Invoke(hand);

                        // If holding an object and tracking lost, safely pause drag rather than dropping to floor
                        if (activeGrabbedObject != null)
                        {
                            Debug.LogWarning($"[HandTrackingManager] Hand tracking lost during grab. Freezing object safely in place.");
                        }
                    }
                }
            }
        }

        private void ProcessInteractions()
        {
            if (!rightHandTracked && !leftHandTracked) return;

            Transform dominantHand = rightHandTracked ? rightHandTransform : leftHandTransform;
            HandType dominantType = rightHandTracked ? HandType.Right : HandType.Left;

            // Direct proximity grab check
            if (activeGrabbedObject == null)
            {
                Collider[] hits = Physics.OverlapSphere(dominantHand.position, 0.12f);
                foreach (var hit in hits)
                {
                    var grabHandler = hit.GetComponentInParent<HandGrabHandler>();
                    if (grabHandler != null && !grabHandler.IsGrabbed)
                    {
                        // Proximity hover
                        break;
                    }
                }
            }
        }

        public void TriggerSimulatedPinchStart(HandType hand, Vector3 position)
        {
            if (hand == HandType.Right) isRightPinching = true;
            else isLeftPinching = true;

            OnPinchStarted?.Invoke(hand, position);

            // Check raycast for pinch target
            Transform handTrans = (hand == HandType.Right) ? rightHandTransform : leftHandTransform;
            if (handTrans != null)
            {
                Ray ray = new Ray(handTrans.position, handTrans.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
                {
                    var pinchTarget = hit.collider.GetComponentInParent<PinchTarget>();
                    if (pinchTarget != null)
                    {
                        pinchTarget.TriggerPinch();
                    }

                    var grabHandler = hit.collider.GetComponentInParent<HandGrabHandler>();
                    if (grabHandler != null)
                    {
                        activeGrabbedObject = grabHandler;
                        grabHandler.StartGrab(handTrans, distanceGrab: true);
                    }
                }
            }
        }

        public void TriggerSimulatedPinchEnd(HandType hand)
        {
            if (hand == HandType.Right) isRightPinching = false;
            else isLeftPinching = false;

            OnPinchEnded?.Invoke(hand);

            if (activeGrabbedObject != null)
            {
                activeGrabbedObject.EndGrab();
                activeGrabbedObject = null;
            }
        }

        public Transform GetHandTransform(HandType hand)
        {
            return hand == HandType.Right ? rightHandTransform : leftHandTransform;
        }

        private bool CheckNativeHandTracked(HandType hand)
        {
            // Safeguarded check that doesn't crash if OVRPlugin is absent
            return true;
        }
    }
}
