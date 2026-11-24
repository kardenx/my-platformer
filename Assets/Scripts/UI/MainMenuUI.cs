using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private string firstLevelScene = "Level01";

    private void Start()
    {
        // нема сейву - нема що продовжувати
        continueButton.interactable = SaveSystem.HasSave;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(
                SaveSystem.HasSave ? continueButton.gameObject : newGameButton.gameObject);
    }

    public void NewGame()
    {
        SaveSystem.Delete();
        GameProgress.Reset();   // статика сама не обнулиться
        Flow.Go(firstLevelScene);
    }

    public void Continue()
    {
        GameProgress.Apply(SaveSystem.Load());

        // 0 - це меню, тому мінімум 1
        Flow.Go(Mathf.Max(1, GameProgress.LevelIndex));
    }

    public void Quit() => Flow.Quit();
}
