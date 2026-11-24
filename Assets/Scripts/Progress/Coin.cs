using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Coin : MonoBehaviour
{
    [SerializeField, Range(1, 10)] private int value = 1;
    [SerializeField] private IntEventSO coinsChanged;

    [Header("Відгук")]
    [SerializeField] private ParticleSystem pickupEffect;   // префаб

    private bool collected;   // щоб не двічі

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        if (!other.TryGetComponent(out PlayerController _)) return;
        collected = true;

        GameProgress.AddCoins(value);
        if (coinsChanged != null) coinsChanged.Raise(GameProgress.Coins);

        // окремо, інакше зникне з монетою
        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
