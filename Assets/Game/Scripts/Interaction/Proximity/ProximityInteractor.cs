using UnityEngine;

namespace KidsVsAliens.Interaction
{
    /// <summary>
    /// Generic marker for actors allowed to activate proximity interactions.
    /// Add once to the player root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProximityInteractor : MonoBehaviour
    {
    }
}
