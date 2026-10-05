using TMPro;
using UnityEngine;
using static InterfaceFactory;

/// <summary>One reusable segmented neon overlay, used for outgoing target and strongest incoming lock.</summary>
public sealed class BikeLaserLockView : MonoBehaviour
{
    private RectTransform root;
    private BikeLaserFrame frame;
    private TMP_Text label;
    public float DisplayedProgress { get; private set; }
    public float CornerSpread { get; private set; }
    public bool Visible => root != null && root.gameObject.activeSelf;
    public void Build(Transform parent, string title)
    {
        root = Rect(parent, title, Vector2.zero, Vector2.one);
        frame = Rect(root, "Segmented Alien Frame", Vector2.one * .5f, Vector2.one * .5f).gameObject.AddComponent<BikeLaserFrame>();
        frame.raycastTarget = false;
        label = Text(root, "LockStatus", "", Vector2.one * .5f, Vector2.one * .5f, 20,
            new Color(1, .65f, .78f), TextAlignmentOptions.Center);
        label.rectTransform.sizeDelta = new Vector2(240, 28);
        root.gameObject.SetActive(false);
    }
    public void Present(AlienBikeLaserWeapon source, AlienBikeController subject, Camera camera, bool incoming)
    {
        if (camera == null || subject == null || source == null || source.Target == null)
        { Hide(); return; }
        bool pulse = source.IsWindingUp;
        Vector3 viewport = camera.WorldToViewportPoint(AlienBikeLaserWeapon.AimPoint(subject) + Vector3.up * .25f);
        if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1)
        { root.gameObject.SetActive(false); return; }
        DisplayedProgress = source.Progress;
        root.gameObject.SetActive(true);
        var canvas = root.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, camera.ViewportToScreenPoint(viewport), uiCamera, out Vector2 center);
        // Size against the live hull on screen. The four corners translate inward;
        // their stroke lengths/thickness never scale like a square texture.
        var hull = subject.GetComponent<BoxCollider>();
        Vector2 low = Vector2.one * float.PositiveInfinity, high = Vector2.one * float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
            // Project the oriented hull, not its inflated world-axis bounding box.
            // World bounds made a diagonally facing bike's final frame almost screen-wide.
            Vector3 point = hull != null ? hull.transform.TransformPoint(hull.center
                + Vector3.Scale(hull.size * .5f + new Vector3(.12f, .3f, .12f), sign))
                : subject.transform.TransformPoint(Vector3.up + Vector3.Scale(new Vector3(1, 1, 2), sign));
            Vector3 screen = camera.WorldToScreenPoint(point);
            if (screen.z <= 0) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 local);
            low = Vector2.Min(low, local); high = Vector2.Max(high, local);
        }
        center = Vector2.Lerp(center, (low + high) * .5f, .8f);
        Vector2 tight = Vector2.Max(new Vector2(52, 58), (high - low) * .5f);
        tight = Vector2.Min(tight, root.rect.size * .25f);
        if (incoming) tight = Vector2.Min(tight, root.rect.size * new Vector2(.18f, .16f));
        float convergence = Mathf.SmoothStep(0, 1, DisplayedProgress);
        Vector2 spread = Vector2.Lerp(incoming ? root.rect.size * new Vector2(.29f, .30f) : tight + new Vector2(110, 85), tight, convergence);
        if (pulse) spread *= 1 + .035f * Mathf.Sin((Time.time - source.LockedAt) / Mathf.Max(.01f, source.dischargeSeconds) * Mathf.PI);
        CornerSpread = spread.x;
        frame.rectTransform.anchoredPosition = center;
        frame.rectTransform.sizeDelta = spread * 2;
        frame.Present(DisplayedProgress, pulse);
        label.text = pulse ? "LOCKED" : incoming ? "INCOMING // LOCK" : "TARGET // LOCK";
        label.rectTransform.anchoredPosition = center + Vector2.up * (spread.y + 24);

    }
    public void Hide() { DisplayedProgress = 0; if (root != null) root.gameObject.SetActive(false); }
}
