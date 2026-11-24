using UnityEngine;

// стрибок на голову
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStomp : MonoBehaviour
{
    [SerializeField] private Transform stompPoint;
    [SerializeField, Range(0.1f, 1f)] private float radius = 0.3f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField, Range(1, 50)] private int damage = 10;
    [SerializeField, Range(1f, 20f)] private float bounceForce = 9f;

    private Rigidbody2D body;
    private PlayerController controller;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        controller = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // тільки коли падаємо
        if (body.linearVelocity.y >= 0f) return;

        Collider2D hit = Physics2D.OverlapCircle(stompPoint.position, radius, enemyLayer);
        if (hit == null) return;

        if (hit.TryGetComponent(out IDamageable target))
        {
            target.TakeDamage(damage, transform.position);

            // через контролер, інакше low jump зрізає відскок
            if (controller != null) controller.Bounce(bounceForce);
            else body.linearVelocity = new Vector2(body.linearVelocity.x, bounceForce);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (stompPoint == null) return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(stompPoint.position, radius);
    }
}
