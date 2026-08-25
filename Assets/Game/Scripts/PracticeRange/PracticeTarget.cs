using System.Collections;
using UnityEngine;

public enum PracticeTargetState
{
    Inactive,
    Active,
    Hardcore
}

public class PracticeTarget : MonoBehaviour
{
    [Header("Hinge")]
    [SerializeField]
    private Vector3 inactiveHingeRotation =
        new Vector3(90f, 0f, 0f);

    [SerializeField]
    private float hingeDuration = 0.5f;

    private Transform hingePivot;
    private Transform piecesRoot;

    private Quaternion activeHingeRotation;

    private Coroutine hingeRoutine;

    private PracticeTargetState state =
        PracticeTargetState.Inactive;

    private bool hasReceivedState;

    public PracticeTargetState State => state;

    private void Awake()
    {
        FindRequiredObjects();

        if (hingePivot != null)
        {
            activeHingeRotation =
                hingePivot.localRotation;
        }
    }

    private void Start()
    {
        // Allows the target to still work
        // if it is ever used without a rail.
        if (!hasReceivedState)
        {
            SetState(
                PracticeTargetState.Inactive,
                false
            );
        }
    }

    private void FindRequiredObjects()
    {
        Transform[] children =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == "HingePivot")
            {
                hingePivot = child;
            }

            if (child.name == "BreakablePieces")
            {
                piecesRoot = child;
            }
        }

        if (hingePivot == null)
        {
            Debug.LogError(
                $"{name}: Could not find 'HingePivot'."
            );
        }

        if (piecesRoot == null)
        {
            Debug.LogError(
                $"{name}: Could not find 'BreakablePieces'."
            );
        }
    }

    public void SetState(
        PracticeTargetState newState,
        bool animate = true
    )
    {
        hasReceivedState = true;
        state = newState;

        if (hingeRoutine != null)
        {
            StopCoroutine(hingeRoutine);
            hingeRoutine = null;
        }

        if (!animate)
        {
            ApplyStateImmediately();
            return;
        }

        // During both raising and lowering,
        // the target remains hittable.
        SetHittable(true);

        hingeRoutine =
            StartCoroutine(
                AnimateHinge()
            );
    }

    private void ApplyStateImmediately()
    {
        if (hingePivot == null)
            return;

        if (state == PracticeTargetState.Inactive)
        {
            hingePivot.localRotation =
                GetInactiveRotation();

            SetHittable(false);
        }
        else
        {
            hingePivot.localRotation =
                activeHingeRotation;

            SetHittable(true);
        }
    }

    private IEnumerator AnimateHinge()
    {
        if (hingePivot == null)
            yield break;

        Quaternion startRotation =
            hingePivot.localRotation;

        Quaternion targetRotation =
            state == PracticeTargetState.Inactive
                ? GetInactiveRotation()
                : activeHingeRotation;

        float elapsed = 0f;

        while (elapsed < hingeDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / hingeDuration
                );

            // Smooth start / stop.
            float smoothT =
                t * t * (3f - 2f * t);

            hingePivot.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    smoothT
                );

            yield return null;
        }

        hingePivot.localRotation =
            targetRotation;

        // Only becomes unhittable
        // AFTER it is fully down.
        if (state == PracticeTargetState.Inactive)
        {
            SetHittable(false);
        }

        hingeRoutine = null;
    }

    private Quaternion GetInactiveRotation()
    {
        return
            activeHingeRotation *
            Quaternion.Euler(
                inactiveHingeRotation
            );
    }

    private void SetHittable(bool hittable)
    {
        if (piecesRoot == null)
            return;

        Collider[] colliders =
            piecesRoot.GetComponentsInChildren<Collider>(
                true
            );

        foreach (Collider pieceCollider in colliders)
        {
            pieceCollider.enabled =
                hittable;
        }
    }
}