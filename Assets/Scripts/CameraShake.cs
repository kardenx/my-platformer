using System.Collections;
using UnityEngine;

// Вішається на Main Camera всередині CameraRig: трясемо localPosition,
// щоб рух батька (стеження на Л4) накладався зверху й нічого не ламав
public class CameraShake : MonoBehaviour
{
    private Vector3 basePosition;
    private Coroutine current;

    private void Awake() => basePosition = transform.localPosition;

    public void Shake(float duration, float strength)
    {
        // Якщо попередня тряска ще йде - зупиняємо, інакше вони складуться
        if (current != null) StopCoroutine(current);
        current = StartCoroutine(ShakeRoutine(duration, strength));
    }

    private IEnumerator ShakeRoutine(float duration, float strength)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Затухання: сильна на початку, згасає до нуля
            float damper = 1f - (elapsed / duration);

            transform.localPosition = basePosition +
                (Vector3)(Random.insideUnitCircle * strength * damper);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localPosition = basePosition;   // обовʼязково повертаємо на місце
        current = null;
    }
}
