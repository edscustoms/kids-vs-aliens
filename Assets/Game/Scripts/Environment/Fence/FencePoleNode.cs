using UnityEngine;

namespace KidsVsAliens.Environment
{
    public sealed class FencePoleNode : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int nodeId;

        public int NodeId => nodeId;

        public void Initialize(int id)
        {
            nodeId = id;
        }
    }
}
