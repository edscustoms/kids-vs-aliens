using System.Collections.Generic;
using UnityEngine;

// Player-scoped observer of registered, baked manual-hoist surfaces. Never creates gameplay zones.
[DisallowMultipleComponent]
public sealed class BeamHoistZonePresentation : MonoBehaviour
{
    [SerializeField] private BeamHoistZoneVFX zonePrefab;
    [Header("Discovery")]
    [SerializeField, Min(0)] private float revealDistance = 6f;
    [SerializeField, Min(0)] private float hideDistance = 7f;
    [SerializeField, Min(0)] private float revealFadeDuration = .4f;
    [SerializeField, Min(0)] private float hideFadeDuration = .35f;
    private sealed class View
    {
        public BeamHoistSurface surface;
        public BeamHoistZoneVFX.Patch[] patches;
        public BeamHoistZoneVFX effect;
        public bool revealed, active;
        public float nextActiveCheck;
        public Vector3 checkedRoot;
    }
    private readonly List<View> views = new();
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private BeamHoistAbility ability;
    private BeamTransportController transport;
    private float clock, nextSync;
    private void Awake()
    {
        ability = GetComponent<BeamHoistAbility>(); transport = GetComponent<BeamTransportController>();
    }
    private void LateUpdate() => TickPresentation(Time.deltaTime);

    public void TickPresentation(float deltaTime)
    {
        clock += Mathf.Max(0, deltaTime);
        if (ability == null || transport == null || zonePrefab == null) return;
        if (clock >= nextSync) { Synchronize(); nextSync = clock + .5f; }
        if (!ability.CanPreviewHoist) { HideImmediately(); return; }

        Vector3 feet = transform.position + Vector3.up * transport.FeetOffset;
        foreach (var view in views)
        {
            if (view.surface == null || !view.surface.isActiveAndEnabled || !view.surface.IsBaked)
            { HideImmediately(view); continue; }
            Vector3 localFeet = view.surface.transform.InverseTransformPoint(feet);
            float nearest = float.PositiveInfinity;
            bool inside = false;
            for (int i = 0; i < view.patches.Length; i++)
            {
                Bounds bounds = view.patches[i].bounds;
                // The lower start band excludes the destination/top from discovery.
                if (localFeet.y <= bounds.max.y)
                    nearest = Mathf.Min(nearest, Vector3.Distance(feet,
                        view.surface.transform.TransformPoint(bounds.ClosestPoint(localFeet))));
                inside |= view.surface.TryGetCandidate(transform.position, i, transport.FeetOffset, out _, out _);
            }
            if (!view.revealed && nearest <= revealDistance) view.revealed = true;
            else if (view.revealed && nearest > Mathf.Max(hideDistance, revealDistance)) view.revealed = false;

            // Runtime validation affects the power-up state ONLY, never the mesh or its bounds.
            if (!inside) { view.active = false; view.nextActiveCheck = 0; }
            else if (clock >= view.nextActiveCheck || view.checkedRoot != transform.position)
            {
                view.active = false;
                for (int i = 0; i < view.patches.Length && !view.active; i++)
                    view.active = ability.CanPreviewSurfaceHoist(view.surface, i, transform.position);
                view.checkedRoot = transform.position; view.nextActiveCheck = clock + .15f;
            }

            if (view.effect == null && view.revealed)
            {
                // Build the ENTIRE baked area once, independent of distance, approach direction,
                // live route checks and which cell Amy occupies. Reuse it for every later reveal.
                var groundPatches = ProjectFootprint(view);
                view.effect = Instantiate(zonePrefab, view.surface.transform);
                view.effect.name = "Manual Hoist Start Zone";
                view.effect.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                view.effect.transform.localScale = Vector3.one;
                view.effect.SetFootprint(groundPatches);
            }
            if (view.effect == null) continue;
            var state = !view.revealed ? BeamHoistZoneState.Hidden : view.active ? BeamHoistZoneState.Active : BeamHoistZoneState.Available;
            view.effect.Present(state, deltaTime, revealFadeDuration, hideFadeDuration, float.IsPositiveInfinity(nearest));
        }
    }

    private void Synchronize()
    {
        for (int i = views.Count - 1; i >= 0; i--)
            if (views[i].surface == null || !BeamHoistSurface.Active.Contains(views[i].surface))
            { RemoveEffect(views[i]); views.RemoveAt(i); }
        foreach (var surface in BeamHoistSurface.Active)
        {
            if (surface == null || !surface.IsBaked || surface.gameObject.scene != gameObject.scene) continue;
            bool exists = false;
            foreach (var view in views) if (view.surface == surface) { exists = true; break; }
            if (exists) continue;
            var patches = new BeamHoistZoneVFX.Patch[surface.CandidateCount];
            for (int i = 0; i < patches.Length; i++) patches[i].bounds = surface.GetBakedApproach(i).region;
            views.Add(new View { surface = surface, patches = patches });
        }
    }
    private List<BeamHoistZoneVFX.Patch> ProjectFootprint(View view)
    {
        var groundPatches = new List<BeamHoistZoneVFX.Patch>();
        for (int i = 0; i < view.patches.Length; i++)
        {
            Bounds source = view.patches[i].bounds;
            Vector3 scale = view.surface.transform.lossyScale;
            int columns = Mathf.Max(1, Mathf.CeilToInt(source.size.x * Mathf.Abs(scale.x) / .4f));
            int rows = Mathf.Max(1, Mathf.CeilToInt(source.size.z * Mathf.Abs(scale.z) / .4f));
            // Tessellation follows uneven ground without altering the source perimeter or glyphs.
            for (int x = 0; x < columns; x++)
                for (int z = 0; z < rows; z++)
                {
                    Vector3 size = new Vector3(source.size.x / columns, source.size.y, source.size.z / rows);
                    Vector3 center = new Vector3(source.min.x + (x + .5f) * size.x, source.center.y, source.min.z + (z + .5f) * size.z);
                    Bounds b = new Bounds(center, size);
                    groundPatches.Add(new BeamHoistZoneVFX.Patch { bounds = b,
                        a = Project(view.surface, b, b.min.x, b.min.z), b = Project(view.surface, b, b.max.x, b.min.z),
                        c = Project(view.surface, b, b.max.x, b.max.z), d = Project(view.surface, b, b.min.x, b.max.z) });
                }
        }
        return groundPatches;
    }
    // One-time ground alignment changes only vertex height. Never omit, expand or trim baked cells.
    private Vector3 Project(BeamHoistSurface surface, Bounds bounds, float x, float z)
    {
        Vector3 top = surface.transform.TransformPoint(new Vector3(x, bounds.max.y, z)) + Vector3.up * .05f;
        float length = bounds.size.y * Mathf.Abs(surface.transform.lossyScale.y) + .1f;
        int count = Physics.RaycastNonAlloc(top, Vector3.down, groundHits, length, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        // The baker's lower band starts .35m below the supported floor.
        Vector3 local = new Vector3(x, bounds.min.y + .375f / Mathf.Abs(surface.transform.lossyScale.y), z);
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.collider is CharacterController || hit.collider.transform.IsChildOf(transform)
                || (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                || hit.collider.gameObject.scene != gameObject.scene || hit.normal.y < .7f || hit.distance >= nearest) continue;
            nearest = hit.distance;
            // Preserve exact baked X/Z even on a rotated prop.
            local.y = surface.transform.InverseTransformPoint(hit.point + Vector3.up * .025f).y;
        }
        return local;
    }
    private static void RemoveEffect(View view)
    {
        if (view.effect == null) return;
        if (Application.isPlaying) Destroy(view.effect.gameObject); else DestroyImmediate(view.effect.gameObject);
    }
    private static void HideImmediately(View view)
    {
        view.revealed = false; view.active = false; view.nextActiveCheck = 0;
        if (view.effect != null) view.effect.Present(BeamHoistZoneState.Hidden, 0, 0, 0, true);
    }
    private void HideImmediately() { foreach (var view in views) HideImmediately(view); }
    private void OnDisable() => HideImmediately();
    private void OnDestroy() { foreach (var view in views) RemoveEffect(view); views.Clear(); }
}
