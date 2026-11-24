using UnityEngine;

// Небезпека: повертає на останню контрольну точку будь-кого, у кого є PlayerRespawn
public class Hazard : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<PlayerRespawn>(out var respawn))
            respawn.Respawn();
    }
}
