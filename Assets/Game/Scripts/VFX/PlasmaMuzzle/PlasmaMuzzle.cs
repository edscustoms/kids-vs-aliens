using UnityEngine;

public class PlasmaMuzzleVFX : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem particles;

    [SerializeField]
    private Color defaultColor = Color.magenta;

    private bool playing;
    private int startedFrame;

    public void Play(
        Color? auraColor = null
    )
    {
        if (particles == null)
        {
            VfxPool.Release(
                this
            );

            return;
        }

        Color color =
            auraColor ?? defaultColor;

        ParticleSystem.MainModule main =
            particles.main;

        main.startColor =
            color;

        particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        particles.Play(
            true
        );

        playing = true;
        startedFrame = Time.frameCount;
    }

    private void Update()
    {
        if (!playing)
            return;

        // Wait at least one frame after Play() before checking IsAlive.
        if (Time.frameCount <= startedFrame)
            return;

        if (particles.IsAlive(true))
            return;

        playing = false;

        VfxPool.Release(
            this
        );
    }

    private void OnDisable()
    {
        playing = false;

        if (particles != null)
        {
            particles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }
}
