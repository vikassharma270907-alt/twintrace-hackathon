using System;
using UnityEngine;
using RecallRoom.Spatial;
using RecallRoom.Data;

namespace RecallRoom.Interaction
{
    [RequireComponent(typeof(SpatialCardAnchor))]
    public class HandGrabHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpatialCardAnchor cardAnchor;
        [SerializeField] private Rigidbody cardRigidbody;

        [Header("Grabbing Config")]
        [SerializeField] private float directGrabRadius = 0.15f;
        [SerializeField] private float snapSurfaceSearchRadius = 0.25f;
        [SerializeField] private float followLerpSpeed = 20f;

        [Header("State")]
        [SerializeField] private bool isGrabbed;
        [SerializeField] private bool isDistancePinch;

        private Transform grabberTransform;
        private Vector3 grabLocalOffset;
        private Quaternion grabLocalRotation;

        public bool IsGrabbed => isGrabbed;

        public event Action<HandGrabHandler> OnGrabStarted;
        public event Action<HandGrabHandler> OnGrabEnded;

        private void Awake()
        {
            if (cardAnchor == null)
            {
                cardAnchor = GetComponent<SpatialCardAnchor>();
            }
            if (cardRigidbody == null)
            {
                cardRigidbody = GetComponent<Rigidbody>();
            }
        }

        private void Update()
        {
            if (isGrabbed && grabberTransform != null)
            {
                UpdateGrabPosition();
            }
        }

        public void StartGrab(Transform grabber, bool distanceGrab = false)
        {
            if (isGrabbed || grabber == null) return;

            isGrabbed = true;
            isDistancePinch = distanceGrab;
            grabberTransform = grabber;

            if (cardRigidbody != null)
            {
                cardRigidbody.isKinematic = true;
            }

            grabLocalOffset = transform.position - grabber.position;
            grabLocalRotation = Quaternion.Inverse(grabber.rotation) * transform.rotation;

            if (cardAnchor != null)
            {
                cardAnchor.SetState(CardAnchorState.Dragging);
            }

            OnGrabStarted?.Invoke(this);
        }

        public void EndGrab()
        {
            if (!isGrabbed) return;

            isGrabbed = false;
            grabberTransform = null;

            // Attempt surface snapping
            AttemptSurfaceSnap();

            OnGrabEnded?.Invoke(this);
        }

        private void UpdateGrabPosition()
        {
            Vector3 targetPosition = grabberTransform.position + grabLocalOffset;
            Quaternion targetRotation = grabberTransform.rotation * grabLocalRotation;

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followLerpSpeed);
        }

        private void AttemptSurfaceSnap()
        {
            if (RoomSurfaceDetector.Instance != null &&
                RoomSurfaceDetector.Instance.SnapToNearestSurface(transform.position, snapSurfaceSearchRadius, out Vector3 snappedPoint, out Vector3 snappedNormal, out SurfaceType surfaceType))
            {
                Quaternion snappedRotation;
                if (surfaceType == SurfaceType.Table)
                {
                    Vector3 lookDir = (Camera.main != null ? Camera.main.transform.position : Vector3.zero) - snappedPoint;
                    lookDir.y = 0f;
                    snappedRotation = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir, Vector3.up) : Quaternion.identity;
                }
                else if (surfaceType == SurfaceType.Wall)
                {
                    snappedRotation = Quaternion.LookRotation(snappedNormal, Vector3.up);
                }
                else
                {
                    snappedRotation = transform.rotation;
                }

                if (cardAnchor != null)
                {
                    cardAnchor.SnapTo(snappedPoint, snappedRotation, surfaceType, true);
                }
            }
            else
            {
                // Leave placed at current release position
                if (cardAnchor != null)
                {
                    cardAnchor.SetState(CardAnchorState.Placed);
                }
            }
        }
    }
}
