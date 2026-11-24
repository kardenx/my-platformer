using UnityEngine;

public class PlayerHealth : Health
{
    [SerializeField] private PlayerRespawn respawn;
    [SerializeField] private PlayerController controller;

    // контролер глушить відкидання
    [SerializeField, Range(0f, 0.5f)] private float knockbackStun = 0.2f;

    [Header("Канали подій")]
    [SerializeField] private IntEventSO healthChanged;
    [SerializeField] private IntEventSO maxHealthChanged;

    // start бо hud вже підписався в onenable
    private void Start()
    {
        if (maxHealthChanged != null) maxHealthChanged.Raise(Max);
        if (healthChanged != null) healthChanged.Raise(Current);
    }

    protected override void OnHealthChanged()
    {
        if (healthChanged != null) healthChanged.Raise(Current);
    }

    protected override void OnDamaged()
    {
        if (controller != null) controller.Stun(knockbackStun);
    }

    protected override void Die()
    {
        respawn.Respawn();
        ResetHealth();   // сам кине подію
    }
}
