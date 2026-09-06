using UnityEngine;

namespace KidsVsAliens.Environment
{
    public sealed class FencePoleNode : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int nodeId;

        [SerializeField, HideInInspector]
        private Vector2Int gridCoordinate;

        [SerializeField, HideInInspector]
        private bool hasGridCoordinate;

        public int NodeId => nodeId;
        public Vector2Int GridCoordinate => gridCoordinate;
        public bool HasGridCoordinate => hasGridCoordinate;

        public void Initialize(
            int id,
            Vector2Int coordinate)
        {
            nodeId = id;
            SetGridCoordinate(coordinate);
        }

        public void SetGridCoordinate(
            Vector2Int coordinate)
        {
            gridCoordinate = coordinate;
            hasGridCoordinate = true;
        }
    }
}
