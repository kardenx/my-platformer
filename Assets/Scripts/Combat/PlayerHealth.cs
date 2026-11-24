using UnityEngine;

public class PlayerHealth : Health
{
    [SerializeField] private PlayerRespawn respawn;            // з лабораторної 4
    [SerializeField] private PlayerController controller;

    // Контролер щокадру задає горизонтальну швидкість і «зʼїдає» відкидання,
    // тому на мить віддаємо тіло фізиці
    [SerializeField, Range(0f, 0.5f)] private float knockbackStun = 0.2f;

    protected override void OnDamaged()
    {
        if (controller != null) controller.Stun(knockbackStun);
    }

    protected override void Die()
    {
        // Гравець не знищується - повертається на контрольну точку
        respawn.Respawn();
        ResetHealth();
    }
}
