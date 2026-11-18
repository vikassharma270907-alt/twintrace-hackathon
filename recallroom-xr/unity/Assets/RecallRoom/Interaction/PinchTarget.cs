using System;
using UnityEngine;
using UnityEngine.Events;

namespace RecallRoom.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class PinchTarget : MonoBehaviour
    {
        [Header("Pinch Sensitivity")]
        [Range(0f, 1f)]
        [SerializeField] private float pinchThreshold = 0.7f;
        [SerializeField] private float hoverScaleFactor = 1.06f;
        [SerializeField] private float animationSpeed = 10f;

        [Header("Hold Action (Optional)")]
        [SerializeField] private bool requireHold = false;
        [SerializeField] private float holdDuration = 1.0f;

        [Header("Events")]
        public UnityEvent onHoverEnter;
        public UnityEvent onHoverExit;
        public UnityEvent onPinchStart;
        public UnityEvent onPinchEnd;
        public UnityEvent onPinchActivated;

        private Vector3 initialScale;
        private Vector3 targetScale;
        private bool isHovered;
        private bool isPinching;
        private float holdTimer;

        public bool IsHovered => isHovered;
        public bool IsPinching => isPinching;
        public float PinchProgress => requireHold ? Mathf.Clamp01(holdTimer / holdDuration) : (isPinching ? 1f : 0f);

        private void Awake()
        {
            initialScale = transform.localScale;
            targetScale = initialScale;
        }

        private void Update()
        {
            // Smooth scale interpolation
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);

            if (isPinching && requireHold)
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= holdDuration)
                {
                    holdTimer = 0f;
                    isPinching = false;
                    onPinchActivated?.Invoke();
                }
            }
        }

        public void OnPointerEnter()
        {
            if (isHovered) return;
            isHovered = true;
            targetScale = initialScale * hoverScaleFactor;
            onHoverEnter?.Invoke();
        }

        public void OnPointerExit()
        {
            if (!isHovered) return;
            isHovered = false;
            if (!isPinching)
            {
                targetScale = initialScale;
            }
            onHoverExit?.Invoke();
        }

        public void OnPinchDown()
        {
            isPinching = true;
            holdTimer = 0f;
            targetScale = initialScale * (hoverScaleFactor * 0.95f);
            onPinchStart?.Invoke();

            if (!requireHold)
            {
                onPinchActivated?.Invoke();
            }
        }

        public void OnPinchUp()
        {
            isPinching = false;
            holdTimer = 0f;
            targetScale = isHovered ? (initialScale * hoverScaleFactor) : initialScale;
            onPinchEnd?.Invoke();
        }
    }
}
