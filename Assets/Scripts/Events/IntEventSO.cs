using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Events/Int Event", fileName = "NewIntEvent")]
public class IntEventSO : ScriptableObject
{
    // ззовні тільки підписатись/відписатись
    private event Action<int> listeners;

    public void Raise(int value) => listeners?.Invoke(value);

    public void Register(Action<int> callback)   => listeners += callback;
    public void Unregister(Action<int> callback) => listeners -= callback;

    // ассет живе між запусками в редакторі
    private void OnDisable() => listeners = null;
}
