using UnityEngine;

public sealed class GameplayPresentationLifetime : MonoBehaviour
{
    [SerializeField]
    private PlayerCharacter player;

    private void LateUpdate()
    {
        if (player == null)
            Destroy(gameObject);
    }
}
