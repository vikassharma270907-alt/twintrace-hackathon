using System;
using System.Collections.Generic;
using UnityEngine;
using RecallRoom.Data;

namespace RecallRoom.Spatial
{
    public struct SurfaceHitResult
    {
        public bool hasHit;
        public Vector3 point;
        public Vector3 normal;
        public SurfaceType surfaceType;
        public Transform hitTransform;
        public float distance;
    }

    public class RoomSurfaceDetector : MonoBehaviour
    {
        public static RoomSurfaceDetector Instance { get; private set; }

        [Header("Layer Masks")]
        [SerializeField] private LayerMask spatialSurfaceMask = ~0;

        [Header("Surface Thresholds")]
        [Range(0.5f, 0.95f)]
        [SerializeField] private float horizontalNormalThreshold = 0.7f;
        [Range(0.05f, 0.5f)]
        [SerializeField] private float verticalNormalThreshold = 0.35f;

        [Header("Fallback Integration")]
        [SerializeField] private TabletopFallbackManager fallbackManager;

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

            if (fallbackManager == null)
            {
                fallbackManager = GetComponent<TabletopFallbackManager>() ?? FindObjectOfType<TabletopFallbackManager>();
            }
        }

        public bool TryFindSurface(Ray ray, out SurfaceHitResult result, float maxDistance = 5.0f)
        {
            result = new SurfaceHitResult
            {
                hasHit = false,
                point = Vector3.zero,
                normal = Vector3.up,
                surfaceType = SurfaceType.Unknown,
                hitTransform = null,
                distance = 0f
            };

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, spatialSurfaceMask))
            {
                result.hasHit = true;
                result.point = hit.point;
                result.normal = hit.normal;
                result.hitTransform = hit.transform;
                result.distance = hit.distance;
                result.surfaceType = ClassifyNormal(hit.normal);
                return true;
            }

            return false;
        }

        public SurfaceType ClassifyNormal(Vector3 normal)
        {
            float upDot = Vector3.Dot(normal.normalized, Vector3.up);

            if (upDot >= horizontalNormalThreshold)
            {
                return SurfaceType.Table;
            }
            if (upDot <= -horizontalNormalThreshold)
            {
                return SurfaceType.Ceiling;
            }
            if (Mathf.Abs(upDot) <= verticalNormalThreshold)
            {
                return SurfaceType.Wall;
            }

            return SurfaceType.Floating;
        }

        public List<ConceptPlacement> DistributeCardsOnSurfaces(List<ConceptCardData> cards, Transform userHead)
        {
            var placements = new List<ConceptPlacement>();
            if (cards == null || cards.Count == 0) return placements;

            // Attempt MRUK / Scene raycast discovery first
            // If fallback manager exists, use it as reliable baseline
            if (fallbackManager != null)
            {
                var fallbackPlacements = fallbackManager.GenerateFallbackPlacements(cards.Count, userHead);
                for (int i = 0; i < cards.Count; i++)
                {
                    if (i < fallbackPlacements.Count)
                    {
                        var fp = fallbackPlacements[i];
                        fp.CardId = cards[i].Id;
                        placements.Add(fp);
                    }
                }
            }

            return placements;
        }

        public bool SnapToNearestSurface(Vector3 inputPoint, float searchRadius, out Vector3 snappedPoint, out Vector3 snappedNormal, out SurfaceType type)
        {
            snappedPoint = inputPoint;
            snappedNormal = Vector3.up;
            type = SurfaceType.Floating;

            // Downward raycast for table snap
            Ray downRay = new Ray(inputPoint + (Vector3.up * 0.1f), Vector3.down);
            if (TryFindSurface(downRay, out SurfaceHitResult downHit, searchRadius + 0.1f))
            {
                if (downHit.surfaceType == SurfaceType.Table || downHit.surfaceType == SurfaceType.Floor)
                {
                    snappedPoint = downHit.point;
                    snappedNormal = downHit.normal;
                    type = downHit.surfaceType;
                    return true;
                }
            }

            // Radial raycast for wall snap
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            foreach (var dir in directions)
            {
                Ray wallRay = new Ray(inputPoint, dir);
                if (TryFindSurface(wallRay, out SurfaceHitResult wallHit, searchRadius))
                {
                    if (wallHit.surfaceType == SurfaceType.Wall)
                    {
                        snappedPoint = wallHit.point;
                        snappedNormal = wallHit.normal;
                        type = SurfaceType.Wall;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
