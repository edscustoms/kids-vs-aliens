using UnityEngine;

public class PlasmaCoreSetup : MonoBehaviour
{
    [SerializeField]
    private PlasmaCoreController plasmaCore;

    [SerializeField]
    private PlasmaCoreConfig config;

    private void Awake()
    {
        Configure();
    }

    public void Configure(Color? auraColor = null)
    {
        if (plasmaCore == null)
            return;

        plasmaCore.Configure(config, auraColor);
    }
}
