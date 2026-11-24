using UnityEngine;

public class LifecycleLogger : MonoBehaviour
{
    [SerializeField] private string label = "A";

    private int updateCount;

    private void Awake() => Log("Awake");

    private void OnEnable() => Log("OnEnable");

    private void Start() => Log("Start");

    private void Update()
    {
        // тільки 3 кадри
        if (updateCount < 3)
        {
            updateCount++;
            Log($"Update #{updateCount}");
        }
    }

    private void OnDisable() => Log("OnDisable");

    private void OnDestroy() => Log("OnDestroy");

    private void Log(string stage)
    {
        Debug.Log($"[кадр {Time.frameCount:D3}] {label} → {stage}", this);
    }
}
