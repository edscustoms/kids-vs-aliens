using UnityEngine;

namespace KidsVsAliens.Environment
{
    [DisallowMultipleComponent]
    public sealed class ChestVisualRig : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform lidPivot;

        [Header("Anchors")]
        [SerializeField] private Transform interactionAnchor;
        [SerializeField] private Transform lootSpawnAnchor;

        [Header("Rarity Glow")]
        [SerializeField] private Renderer[] rarityGlowRenderers;

        public Transform VisualRoot => visualRoot;
        public Transform LidPivot => lidPivot;
        public Transform InteractionAnchor => interactionAnchor;
        public Transform LootSpawnAnchor => lootSpawnAnchor;
        public Renderer[] RarityGlowRenderers => rarityGlowRenderers;

        public void Configure(
            Transform newVisualRoot,
            Transform newLidPivot,
            Transform newInteractionAnchor,
            Transform newLootSpawnAnchor,
            Renderer[] newRarityGlowRenderers)
        {
            visualRoot = newVisualRoot;
            lidPivot = newLidPivot;
            interactionAnchor = newInteractionAnchor;
            lootSpawnAnchor = newLootSpawnAnchor;
            rarityGlowRenderers = newRarityGlowRenderers;
        }
    }
}
