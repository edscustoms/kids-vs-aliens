using UnityEngine;

namespace KidsVsAliens.Environment
{
    public sealed class FenceRunSegment : MonoBehaviour
    {
        [SerializeField, HideInInspector] private FenceRun owner;
        [SerializeField, HideInInspector] private FencePoleNode nodeA;
        [SerializeField, HideInInspector] private FencePoleNode nodeB;

        public FenceRun Owner => owner;
        public FencePoleNode NodeA => nodeA;
        public FencePoleNode NodeB => nodeB;

        public void Initialize(
            FenceRun run,
            FencePoleNode a,
            FencePoleNode b)
        {
            owner = run;
            nodeA = a;
            nodeB = b;
        }
    }
}
