using UnityEngine;

// Памʼятає останню контрольну точку й повертає на неї
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerRespawn : MonoBehaviour
{
    private Vector3 checkpoint;
    private Rigidbody2D body;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        checkpoint = transform.position;   // старт рівня - перша точка
    }

    public void SetCheckpoint(Vector3 position) => checkpoint = position;

    public void Respawn()
    {
        transform.position = checkpoint;

        // Обовʼязково: інакше персонаж зʼявиться з накопиченою
        // швидкістю падіння й одразу полетить униз
        body.linearVelocity = Vector2.zero;
    }
}
