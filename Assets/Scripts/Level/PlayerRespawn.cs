using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerRespawn : MonoBehaviour
{
    private Vector3 checkpoint;
    private Rigidbody2D body;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        checkpoint = transform.position;
    }

    public void SetCheckpoint(Vector3 position) => checkpoint = position;

    public void Respawn()
    {
        transform.position = checkpoint;

        // скидаємо швидкість, бо падав би далі
        body.linearVelocity = Vector2.zero;
    }
}
