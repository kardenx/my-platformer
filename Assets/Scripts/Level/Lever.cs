using UnityEngine;

// Важіль знає, ЩО станеться (вмикає ціль); гравець знає лише, КОЛИ натиснути
public class Lever : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject target;   // платформа, двері, міст
    [SerializeField] private SpriteRenderer handle;  // необовʼязково: віддзеркалюємо ручку

    private bool isOn;

    private void Start() => isOn = target != null && target.activeSelf;

    public void Interact()
    {
        isOn = !isOn;
        if (target != null) target.SetActive(isOn);
        if (handle != null) handle.flipX = isOn;   // видно, у якому положенні важіль
    }
}
