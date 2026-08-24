using UnityEngine;

public class PlasmaImpactVFX : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem particles;

    [SerializeField]
    private Color defaultColor = Color.magenta;

    public void Play(Color? auraColor = null)
    {
        if (particles == null)
            return;

        Color color = auraColor ?? defaultColor;

        ParticleSystem.MainModule main = particles.main;
        main.startColor = color;

        particles.Play();

        Destroy(
            gameObject,
            main.duration + main.startLifetime.constantMax
        );
    }
}