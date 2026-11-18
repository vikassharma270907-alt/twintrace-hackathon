using System;
using UnityEngine;

namespace RecallRoom.Data
{
    public enum SurfaceType
    {
        Unknown = 0,
        Table = 1,
        Wall = 2,
        Floor = 3,
        Ceiling = 4,
        Floating = 5,
        TabletopFallback = 6
    }

    [Serializable]
    public class ConceptPlacement
    {
        [SerializeField] private string cardId;
        [SerializeField] private Vector3 position;
        [SerializeField] private Quaternion rotation = Quaternion.identity;
        [SerializeField] private Vector3 surfaceNormal = Vector3.up;
        [SerializeField] private SurfaceType surfaceType = SurfaceType.TabletopFallback;
        [SerializeField] private bool isAnchored;
        [SerializeField] private string anchorId;

        public string CardId { get => cardId; set => cardId = value; }
        public Vector3 Position { get => position; set => position = value; }
        public Quaternion Rotation { get => rotation; set => rotation = value; }
        public Vector3 SurfaceNormal { get => surfaceNormal; set => surfaceNormal = value; }
        public SurfaceType SurfaceType { get => surfaceType; set => surfaceType = value; }
        public bool IsAnchored { get => isAnchored; set => isAnchored = value; }
        public string AnchorId { get => anchorId; set => anchorId = value; }

        public ConceptPlacement() { }

        public ConceptPlacement(string cardId, Vector3 position, Quaternion rotation, Vector3 surfaceNormal, SurfaceType surfaceType, bool isAnchored = false, string anchorId = null)
        {
            this.cardId = cardId;
            this.position = position;
            this.rotation = rotation;
            this.surfaceNormal = surfaceNormal;
            this.surfaceType = surfaceType;
            this.isAnchored = isAnchored;
            this.anchorId = anchorId;
        }

        public static ConceptPlacement CreateTablePlacement(string cardId, Vector3 pos, Vector3 userHeadPos)
        {
            Vector3 lookDirection = userHeadPos - pos;
            lookDirection.y = 0;
            Quaternion rot = lookDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDirection, Vector3.up) : Quaternion.identity;
            return new ConceptPlacement(cardId, pos, rot, Vector3.up, SurfaceType.Table, true);
        }

        public static ConceptPlacement CreateWallPlacement(string cardId, Vector3 pos, Vector3 wallNormal)
        {
            Quaternion rot = Quaternion.LookRotation(wallNormal, Vector3.up);
            return new ConceptPlacement(cardId, pos, rot, wallNormal, SurfaceType.Wall, true);
        }
    }
}
