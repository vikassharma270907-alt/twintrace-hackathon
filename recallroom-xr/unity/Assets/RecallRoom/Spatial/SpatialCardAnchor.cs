using System;
using System.Collections;
using UnityEngine;
using RecallRoom.Data;

namespace RecallRoom.Spatial
{
    public enum CardAnchorState
    {
        Unplaced = 0,
        Dragging = 1,
        Placed = 2,
        ActiveRecall = 3,
        Evaluated = 4
    }

    [SelectionBase]
    public class SpatialCardAnchor : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private CardAnchorState state = CardAnchorState.Unplaced;
        [SerializeField] private string cardId;
        [SerializeField] private SurfaceType currentSurfaceType = SurfaceType.TabletopFallback;

        [Header("Position Recovery Safeguard")]
        [SerializeField] private Vector3 originPosition;
        [SerializeField] private Quaternion originRotation;
        [SerializeField] private float outOfBoundsDistance = 5.0f;
        [SerializeField] private float floorYThreshold = -2.0f;

        [Header("Alignment & Snapping")]
        [SerializeField] private bool billboardToUser = true;
        [SerializeField] private float billboardTurnRate = 5f;
        [SerializeField] private float snapLerpSpeed = 12f;

        [Header("Visual Feedback")]
        [SerializeField] private Renderer cardRenderer;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(0.3f, 0.7f, 1.0f);
        [SerializeField] private Color activeRecallColor = new Color(1.0f, 0.85f, 0.2f);

        private Transform userCameraTransform;
        private Coroutine snapCoroutine;

        public CardAnchorState State => state;
        public string CardId => cardId;
        public SurfaceType CurrentSurfaceType => currentSurfaceType;
        public Vector3 OriginPosition => originPosition;

        public event Action<SpatialCardAnchor, CardAnchorState> OnStateChanged;

        private void Awake()
        {
            originPosition = transform.position;
            originRotation = transform.rotation;
        }

        private void Start()
        {
            if (Camera.main != null)
            {
                userCameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            // Safeguard recovery
            CheckBoundsRecovery();

            // Billboarding logic if placed floating or tabletop
            if (billboardToUser && state != CardAnchorState.Dragging && userCameraTransform != null)
            {
                if (currentSurfaceType == SurfaceType.Floating || currentSurfaceType == SurfaceType.TabletopFallback)
                {
                    Vector3 lookDir = userCameraTransform.position - transform.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * billboardTurnRate);
                    }
                }
            }
        }

        public void Initialize(string cardId, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            this.cardId = cardId;
            this.originPosition = spawnPosition;
            this.originRotation = spawnRotation;
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
            SetState(CardAnchorState.Placed);
        }

        public void SetState(CardAnchorState newState)
        {
            if (state == newState) return;
            state = newState;
            UpdateVisualState();
            OnStateChanged?.Invoke(this, state);
        }

        public void SetHovered(bool isHovered)
        {
            if (state == CardAnchorState.ActiveRecall) return;
            if (cardRenderer != null)
            {
                cardRenderer.material.color = isHovered ? hoverColor : normalColor;
            }
        }

        public void SnapTo(Vector3 targetPosition, Quaternion targetRotation, SurfaceType surfaceType, bool animate = true)
        {
            currentSurfaceType = surfaceType;
            if (snapCoroutine != null)
            {
                StopCoroutine(snapCoroutine);
            }

            if (animate)
            {
                snapCoroutine = StartCoroutine(AnimateSnap(targetPosition, targetRotation));
            }
            else
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
                SetState(CardAnchorState.Placed);
            }
        }

        private IEnumerator AnimateSnap(Vector3 targetPos, Quaternion targetRot)
        {
            float elapsed = 0f;
            float duration = 0.25f;
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;
            SetState(CardAnchorState.Placed);
            snapCoroutine = null;
        }

        public void RecoverToOrigin()
        {
            Debug.LogWarning($"[SpatialCardAnchor] Card {cardId} went out of bounds. Recovering to origin: {originPosition}");
            SnapTo(originPosition, originRotation, SurfaceType.TabletopFallback, true);
        }

        private void CheckBoundsRecovery()
        {
            if (transform.position.y < floorYThreshold)
            {
                RecoverToOrigin();
                return;
            }

            if (userCameraTransform != null)
            {
                float dist = Vector3.Distance(transform.position, userCameraTransform.position);
                if (dist > outOfBoundsDistance)
                {
                    RecoverToOrigin();
                }
            }
        }

        private void UpdateVisualState()
        {
            if (cardRenderer == null) return;

            switch (state)
            {
                case CardAnchorState.ActiveRecall:
                    cardRenderer.material.color = activeRecallColor;
                    break;
                case CardAnchorState.Dragging:
                    cardRenderer.material.color = hoverColor;
                    break;
                default:
                    cardRenderer.material.color = normalColor;
                    break;
            }
        }
    }
}
