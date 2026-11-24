using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LevelExit : MonoBehaviour
{
    private bool used;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used || !other.TryGetComponent(out PlayerController _)) return;
        used = true;

        // що далі - вирішує flow
        Flow.NextLevel();
    }
}
