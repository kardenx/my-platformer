using UnityEngine;

public class PlayerHealth : Health
{
    [SerializeField] private PlayerRespawn respawn;
    [SerializeField] private PlayerController controller;

    // контролер глушить відкидання
    [SerializeField, Range(0f, 0.5f)] private float knockbackStun = 0.2f;

    protected override void OnDamaged()
    {
        if (controller != null) controller.Stun(knockbackStun);
    }

    protected override void Die()
    {
        // на чекпоінт
        respawn.Respawn();
        ResetHealth();
    }
}
