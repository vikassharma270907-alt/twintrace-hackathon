using System.Collections.Generic;
using UnityEngine;
using RecallRoom.Data;

namespace RecallRoom.Spatial
{
    public class TabletopFallbackManager : MonoBehaviour
    {
        public static TabletopFallbackManager Instance { get; private set; }

        [Header("Arc Placement Settings")]
        [Tooltip("Distance from user head in meters")]
        [SerializeField] private float arcRadius = 0.85f;

        [Tooltip("Vertical offset from user head in meters")]
        [SerializeField] private float heightOffset = -0.25f;

        [Tooltip("Total angular spread of cards in degrees")]
        [SerializeField] private float arcAngleSpread = 90f;

        [Tooltip("Card upward tilt angle in degrees towards user")]
        [SerializeField] private float tiltTowardsUserDegrees = 25f;

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

        public List<ConceptPlacement> GenerateFallbackPlacements(int cardCount, Transform userHeadTransform = null)
        {
            var placements = new List<ConceptPlacement>();
            if (cardCount <= 0) return placements;

            Vector3 headPos = userHeadTransform != null ? userHeadTransform.position : Camera.main != null ? Camera.main.transform.position : Vector3.up * 1.6f;
            Vector3 forward = userHeadTransform != null ? userHeadTransform.forward : Camera.main != null ? Camera.main.transform.forward : Vector3.forward;

            // Project forward onto horizontal plane
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            if (cardCount == 1)
            {
                Vector3 centerPos = headPos + (forward * arcRadius) + (Vector3.up * heightOffset);
                Quaternion rot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(tiltTowardsUserDegrees, 0f, 0f);
                placements.Add(new ConceptPlacement("card_0", centerPos, rot, Vector3.up, SurfaceType.TabletopFallback, true));
                return placements;
            }

            float stepAngle = arcAngleSpread / (cardCount - 1);
            float startAngle = -arcAngleSpread * 0.5f;

            for (int i = 0; i < cardCount; i++)
            {
                float currentAngle = startAngle + (i * stepAngle);
                // Rotate forward vector around up axis
                Quaternion angleRot = Quaternion.AngleAxis(currentAngle, Vector3.up);
                Vector3 direction = angleRot * forward;
                Vector3 cardPos = headPos + (direction * arcRadius) + (Vector3.up * heightOffset);

                // Look rotation back toward user
                Vector3 lookDir = (headPos - cardPos);
                lookDir.y = 0f;
                Quaternion cardRot = Quaternion.LookRotation(lookDir, Vector3.up) * Quaternion.Euler(tiltTowardsUserDegrees, 0f, 0f);

                placements.Add(new ConceptPlacement($"card_{i}", cardPos, cardRot, Vector3.up, SurfaceType.TabletopFallback, true));
            }

            return placements;
        }

        public Vector3 GetSlotPosition(int slotIndex, int totalSlots, Transform userHeadTransform = null)
        {
            var placements = GenerateFallbackPlacements(Mathf.Max(1, totalSlots), userHeadTransform);
            if (slotIndex >= 0 && slotIndex < placements.Count)
            {
                return placements[slotIndex].Position;
            }
            return Vector3.forward;
        }
    }
}
