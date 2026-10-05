using System;
using System.Collections;
using UnityEngine;

/// <summary>One authored roadside sign. Broken state persists; its short debris flight is cosmetic.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(RunWorldObject))]
public sealed class BikeRouteBreakableSign : MonoBehaviour, IRunStateParticipant
{
    public Rigidbody signBody;
    public BoxCollider approach;
    [Range(.1f, 1)] public float speedFraction = .6f;
    [Range(0, .2f)] public float speedLoss = .08f;
    public bool Broken { get; private set; }
    public string RunStateKey => "bike-route-sign";
    private Vector3 intactPosition;
    private Quaternion intactRotation;
    private Collider solid;
    private Collider[] ignoredBike;
    [Serializable] private struct Saved { public bool broken; }
    private void Awake()
    {
        intactPosition = signBody.transform.localPosition; intactRotation = signBody.transform.localRotation;
        solid = signBody.GetComponent<Collider>();
    }
    private void OnTriggerEnter(Collider other)
    {
        var bike = other.GetComponentInParent<AlienBikeController>();
        if (bike != null) TryBreak(bike);
    }
    // A bike can cross the speed threshold between entering the small lead-in
    // trigger and reaching the post. Stay handles that without a second collision owner.
    private void OnTriggerStay(Collider other) => OnTriggerEnter(other);
    public bool TryBreak(AlienBikeController bike)
    {
        if (Broken || bike == null || bike.Rider == null || !bike.Rider.IsDriving || Time.timeScale <= 0
            || ActiveRunController.Instance != null && !ActiveRunController.Instance.IsReady) return false;
        Vector3 velocity = bike.Body.linearVelocity;
        Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
        if (horizontal.magnitude < bike.maxSpeed * speedFraction) return false;
        Broken = true; approach.enabled = false;
        ignoredBike = bike.GetComponentsInChildren<Collider>();
        foreach (var collider in ignoredBike) Physics.IgnoreCollision(solid, collider, true);
        signBody.isKinematic = false;
        signBody.linearVelocity = horizontal * .55f + Vector3.up * 4;
        signBody.angularVelocity = Vector3.Cross(Vector3.up, horizontal.normalized) * 5;
        bike.Body.linearVelocity = velocity - horizontal * speedLoss;
        bike.GetComponent<BikeRouteImpactFeedback>()?.Present(horizontal.magnitude * speedLoss, horizontal);
        ActiveRunController.Instance?.MarkDirty();
        StartCoroutine(SettleDebris());
        return true;
    }
    private IEnumerator SettleDebris()
    {
        yield return new WaitForSeconds(5);
        HideDebris();
    }
    private void HideDebris()
    {
        signBody.isKinematic = true; signBody.gameObject.SetActive(false);
        ClearIgnoredCollisions();
    }
    private void ClearIgnoredCollisions()
    {
        if (ignoredBike == null) return;
        foreach (var collider in ignoredBike) if (collider != null) Physics.IgnoreCollision(solid, collider, false);
        ignoredBike = null;
    }
    public string CaptureRunState() => JsonUtility.ToJson(new Saved { broken = Broken });
    public void RestoreRunState(string json)
    {
        StopAllCoroutines(); ClearIgnoredCollisions();
        Broken = JsonUtility.FromJson<Saved>(json).broken;
        signBody.isKinematic = true;
        signBody.transform.SetLocalPositionAndRotation(intactPosition, intactRotation);
        signBody.gameObject.SetActive(!Broken); approach.enabled = !Broken;
        // Restoring twice never relaunches debris, slows a bike or requests feedback.
    }
}
