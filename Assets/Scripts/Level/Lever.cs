using UnityEngine;

public class Lever : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject target;
    [SerializeField] private SpriteRenderer handle;

    private bool isOn;

    private void Start() => isOn = target != null && target.activeSelf;

    public void Interact()
    {
        isOn = !isOn;
        if (target != null) target.SetActive(isOn);
        if (handle != null) handle.flipX = isOn;
    }
}
