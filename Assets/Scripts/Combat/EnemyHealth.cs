using UnityEngine;

public class EnemyHealth : Health
{
    [SerializeField] private EnemyController controller;
    [SerializeField] private Collider2D hitbox;
    [SerializeField, Range(0.2f, 3f)] private float destroyDelay = 1.2f;

    protected override void OnDamaged()
    {
        controller.Machine.ChangeState(controller.HurtState);
    }

    protected override void Die()
    {
        controller.Machine.ChangeState(controller.DeadState);

        // щоб труп не бив
        hitbox.enabled = false;
        controller.enabled = false;

        GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;

        Destroy(gameObject, destroyDelay);
    }
}
