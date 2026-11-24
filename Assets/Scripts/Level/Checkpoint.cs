using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // по компоненту, не по тегу
        if (other.TryGetComponent<PlayerRespawn>(out var respawn))
            respawn.SetCheckpoint(transform.position);
    }
}
