using System.Collections;
using System.Collections.Generic;
using KidsVsAliens.Environment;
using UnityEngine;

namespace KidsVsAliens.Environment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ChestVisualRig))]
    public sealed class LootChest : MonoBehaviour
    {
        [Header("Opening")]
        [SerializeField]
        private float openAngle = -110f;

        [SerializeField]
        [Min(0.01f)]
        private float openDuration = 0.65f;

        [SerializeField]
        private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Loot")]
        [Tooltip(
            "World/drop prefabs only. For the current POC assign PlasmaPistol_Dropped. "
                + "The prefab keeps its existing PickupItem behaviour."
        )]
        [SerializeField]
        private GameObject[] possibleLootPrefabs;

        [SerializeField]
        [Range(0, 3)]
        private int minimumLootCount = 1;

        [SerializeField]
        [Range(0, 3)]
        private int maximumLootCount = 1;

        [Header("Loot Placement")]
        [Tooltip(
            "Extra distance past LootSpawnAnchor. This keeps loot away from Amy "
                + "so it does not get picked up instantly."
        )]
        [SerializeField]
        [Min(0f)]
        private float forwardSpawnOffset = 0.55f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Minimum distance kept beyond the chest's own BoxCollider when placing loot.")]
        private float minimumChestClearance = 0.12f;

        [SerializeField]
        [Min(0f)]
        private float itemSpacing = 0.32f;

        [SerializeField]
        [Min(0f)]
        private float minimumPlayerClearance = 0.85f;

        [SerializeField]
        [Min(0f)]
        private float groundOffset = 0.20f;

        [SerializeField]
        [Min(0.1f)]
        private float groundProbeHeight = 1.25f;

        [SerializeField]
        [Min(0.1f)]
        private float groundProbeDistance = 3f;

        [SerializeField]
        private LayerMask groundMask = ~0;

        [Header("POC Test")]
        [Tooltip("Temporary testing option. Leave OFF for normal gameplay.")]
        [SerializeField]
        private bool openOnStart;

        private ChestVisualRig visualRig;

        private Transform lidPivot;
        private Transform lootSpawnAnchor;

        private Quaternion closedLidRotation;

        private bool isOpening;
        private bool isOpen;
        private bool lootSpawned;

        public bool IsOpen => isOpen;
        public bool IsOpening => isOpening;

        private void Awake()
        {
            visualRig = GetComponent<ChestVisualRig>();

            if (visualRig == null)
            {
                Debug.LogError($"{name}: ChestVisualRig is missing.", this);

                enabled = false;
                return;
            }

            lidPivot = visualRig.LidPivot;
            lootSpawnAnchor = visualRig.LootSpawnAnchor;

            if (lidPivot == null)
            {
                Debug.LogError($"{name}: ChestVisualRig has no LidPivot.", this);

                enabled = false;
                return;
            }

            closedLidRotation = lidPivot.localRotation;
        }

        private void Start()
        {
            if (openOnStart)
            {
                Open();
            }
        }

        [ContextMenu("Open Chest")]
        public void Open()
        {
            Open(null);
        }

        public void Open(Transform opener)
        {
            if (!enabled || isOpen || isOpening)
            {
                return;
            }

            StartCoroutine(OpenRoutine(opener));
        }

        private IEnumerator OpenRoutine(Transform opener)
        {
            isOpening = true;

            Quaternion startRotation = lidPivot.localRotation;

            Quaternion targetRotation = closedLidRotation * Quaternion.Euler(openAngle, 0f, 0f);

            float elapsed = 0f;

            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / openDuration);

                float curvedT = openCurve != null ? openCurve.Evaluate(t) : t;

                lidPivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, curvedT);

                yield return null;
            }

            lidPivot.localRotation = targetRotation;

            isOpening = false;
            isOpen = true;

            SpawnLoot(opener);
        }

        private void SpawnLoot(Transform opener)
        {
            if (lootSpawned)
                return;

            lootSpawned = true;

            if (possibleLootPrefabs == null || possibleLootPrefabs.Length == 0)
            {
                return;
            }

            int min = Mathf.Clamp(minimumLootCount, 0, 3);

            int max = Mathf.Clamp(maximumLootCount, min, 3);

            int lootCount = Random.Range(min, max + 1);

            if (lootCount <= 0)
                return;

            Transform player = opener != null ? opener : FindPlayerTransform();

            Vector3 chestPosition = transform.position;

            Vector3 anchorPosition =
                lootSpawnAnchor != null ? lootSpawnAnchor.position : chestPosition;

            Vector3 outward = anchorPosition - chestPosition;

            outward.y = 0f;

            if (outward.sqrMagnitude < 0.001f)
            {
                outward = transform.forward;

                outward.y = 0f;
            }

            if (outward.sqrMagnitude < 0.001f)
                outward = Vector3.forward;

            outward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, outward).normalized;

            List<GameObject> availableLoot = new List<GameObject>();

            foreach (GameObject prefab in possibleLootPrefabs)
            {
                if (prefab != null && !availableLoot.Contains(prefab))
                {
                    availableLoot.Add(prefab);
                }
            }

            lootCount = Mathf.Min(lootCount, availableLoot.Count);

            List<Vector3> placedLootPositions = new List<Vector3>();

            for (int i = 0; i < lootCount; i++)
            {
                int randomIndex = Random.Range(0, availableLoot.Count);

                GameObject prefab = availableLoot[randomIndex];

                // Pick without replacement:
                // one configured prefab can only be chosen once per chest opening.
                availableLoot.RemoveAt(randomIndex);

                float lateral = CalculateLateralOffset(i, lootCount);

                Vector3 candidate = anchorPosition + outward * forwardSpawnOffset + right * lateral;

                candidate = PushAwayFromPlayer(candidate, player, outward);

                candidate = EnsureOutsideChest(candidate, outward);

                candidate = EnsureLootSpacing(candidate, placedLootPositions, right, player);

                Vector3 groundPoint = FindGroundPoint(candidate);

                placedLootPositions.Add(groundPoint);

                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject spawned = Instantiate(prefab, groundPoint, rotation);

                PlaceSpawnedObjectOnGround(spawned, groundPoint.y);
            }
        }

        private float CalculateLateralOffset(int index, int count)
        {
            if (count <= 1)
                return 0f;

            float center = (count - 1) * 0.5f;

            return (index - center) * itemSpacing;
        }

        private Vector3 PushAwayFromPlayer(Vector3 candidate, Transform player, Vector3 outward)
        {
            if (player == null || minimumPlayerClearance <= 0f)
            {
                return candidate;
            }

            Vector3 playerPosition = player.position;

            Vector3 playerToCandidate = candidate - playerPosition;

            playerToCandidate.y = 0f;

            float distance = playerToCandidate.magnitude;

            if (distance >= minimumPlayerClearance)
            {
                return candidate;
            }

            // Never push loot backwards toward the chest.
            // Keep its outward distance and gain player clearance sideways.
            Vector3 right = Vector3.Cross(Vector3.up, outward);

            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;

            right.Normalize();

            float forwardSeparation = Vector3.Dot(playerToCandidate, outward);

            float lateralSeparation = Vector3.Dot(playerToCandidate, right);

            float neededLateralSquared =
                minimumPlayerClearance * minimumPlayerClearance
                - forwardSeparation * forwardSeparation;

            if (neededLateralSquared <= 0f)
                return candidate;

            float targetLateralMagnitude = Mathf.Sqrt(neededLateralSquared);

            float lateralSign;

            if (Mathf.Abs(lateralSeparation) > 0.001f)
            {
                lateralSign = Mathf.Sign(lateralSeparation);
            }
            else
            {
                float playerSide = Vector3.Dot(playerPosition - transform.position, right);

                lateralSign = playerSide >= 0f ? -1f : 1f;
            }

            float desiredLateral = targetLateralMagnitude * lateralSign;

            candidate += right * (desiredLateral - lateralSeparation);

            return candidate;
        }

        private Vector3 EnsureOutsideChest(Vector3 candidate, Vector3 outward)
        {
            BoxCollider chestCollider = GetComponent<BoxCollider>();

            if (chestCollider == null)
                return candidate;

            Vector3 worldCenter = chestCollider.transform.TransformPoint(chestCollider.center);

            Vector3 localDirection = chestCollider.transform.InverseTransformDirection(outward);

            Vector3 halfSize = chestCollider.size * 0.5f;

            Vector3 supportLocal =
                chestCollider.center
                + new Vector3(
                    localDirection.x >= 0f ? halfSize.x : -halfSize.x,
                    localDirection.y >= 0f ? halfSize.y : -halfSize.y,
                    localDirection.z >= 0f ? halfSize.z : -halfSize.z
                );

            Vector3 supportWorld = chestCollider.transform.TransformPoint(supportLocal);

            float colliderFrontDistance = Vector3.Dot(supportWorld - worldCenter, outward);

            float candidateDistance = Vector3.Dot(candidate - worldCenter, outward);

            float requiredDistance = colliderFrontDistance + minimumChestClearance;

            if (candidateDistance < requiredDistance)
            {
                candidate += outward * (requiredDistance - candidateDistance);
            }

            return candidate;
        }

        private Vector3 EnsureLootSpacing(
            Vector3 candidate,
            IReadOnlyList<Vector3> placedPositions,
            Vector3 right,
            Transform player
        )
        {
            if (placedPositions == null || placedPositions.Count == 0 || itemSpacing <= 0f)
            {
                return candidate;
            }

            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;

            right.Normalize();

            Vector3 baseCandidate = candidate;

            // Search nearby lateral slots.
            // This happens AFTER player/chest correction because those
            // corrections can otherwise move two different loot slots
            // onto the same final position.
            const int maxSteps = 8;

            for (int step = 0; step <= maxSteps; step++)
            {
                if (step == 0)
                {
                    if (IsLootPositionValid(baseCandidate, placedPositions, player))
                    {
                        return baseCandidate;
                    }

                    continue;
                }

                Vector3 positive = baseCandidate + right * (itemSpacing * step);

                if (IsLootPositionValid(positive, placedPositions, player))
                {
                    return positive;
                }

                Vector3 negative = baseCandidate - right * (itemSpacing * step);

                if (IsLootPositionValid(negative, placedPositions, player))
                {
                    return negative;
                }
            }

            // Very defensive fallback for an unusually crowded placement.
            return baseCandidate + right * (itemSpacing * (placedPositions.Count + 1));
        }

        private bool IsLootPositionValid(
            Vector3 candidate,
            IReadOnlyList<Vector3> placedPositions,
            Transform player
        )
        {
            foreach (Vector3 placed in placedPositions)
            {
                Vector3 delta = candidate - placed;

                delta.y = 0f;

                if (delta.sqrMagnitude < itemSpacing * itemSpacing)
                {
                    return false;
                }
            }

            if (player != null && minimumPlayerClearance > 0f)
            {
                Vector3 playerDelta = candidate - player.position;

                playerDelta.y = 0f;

                if (playerDelta.sqrMagnitude < minimumPlayerClearance * minimumPlayerClearance)
                {
                    return false;
                }
            }

            return true;
        }

        private Vector3 FindGroundPoint(Vector3 candidate)
        {
            Vector3 rayOrigin = candidate + Vector3.up * groundProbeHeight;

            float rayDistance = groundProbeHeight + groundProbeDistance;

            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore
            );

            bool foundGround = false;
            float nearestDistance = float.PositiveInfinity;

            Vector3 nearestPoint = candidate;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;

                // Never treat this chest as ground for its own loot.
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;

                nearestPoint = hit.point;

                foundGround = true;
            }

            if (foundGround)
            {
                candidate.y = nearestPoint.y;
            }

            return candidate;
        }

        private void PlaceSpawnedObjectOnGround(GameObject spawned, float groundY)
        {
            if (spawned == null)
                return;

            if (!TryGetObjectBottomY(spawned, out float bottomY))
            {
                // Fallback when a pickup has neither a usable
                // non-trigger collider nor a renderer.
                Vector3 fallback = spawned.transform.position;

                fallback.y = groundY + groundOffset;

                spawned.transform.position = fallback;

                return;
            }

            float desiredBottomY = groundY + groundOffset;

            float deltaY = desiredBottomY - bottomY;

            spawned.transform.position += Vector3.up * deltaY;
        }

        private static bool TryGetObjectBottomY(GameObject spawned, out float bottomY)
        {
            bottomY = 0f;
            bool found = false;

            // Prefer real collision geometry. Ignore trigger volumes,
            // because pickup triggers are often much larger than the item.
            Collider[] colliders = spawned.GetComponentsInChildren<Collider>(true);

            foreach (Collider collider in colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                float value = collider.bounds.min.y;

                if (!found || value < bottomY)
                {
                    bottomY = value;
                    found = true;
                }
            }

            if (found)
                return true;

            // Fallback to visible mesh bounds.
            Renderer[] renderers = spawned.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                float value = renderer.bounds.min.y;

                if (!found || value < bottomY)
                {
                    bottomY = value;
                    found = true;
                }
            }

            return found;
        }

        private static Transform FindPlayerTransform()
        {
            PlayerCharacter playerCharacter = Object.FindAnyObjectByType<PlayerCharacter>();

            return playerCharacter != null ? playerCharacter.transform : null;
        }
    }
}
