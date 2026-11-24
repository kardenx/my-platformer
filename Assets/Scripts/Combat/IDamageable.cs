using UnityEngine;

// Межа між тим, хто бʼє, і тим, кого бʼють: урон іде через інтерфейс, а не через тег
public interface IDamageable
{
    void TakeDamage(int amount, Vector2 sourcePosition);
}
