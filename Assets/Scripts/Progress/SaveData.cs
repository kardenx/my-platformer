[System.Serializable]
public class SaveData
{
    public const int CurrentVersion = 2;

    // завжди першим
    public int version = CurrentVersion;

    public int coins;        // v1
    public int levelIndex;   // v2
}
