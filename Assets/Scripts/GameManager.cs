using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private LevelDatabase levelDatabase;

    private GameObject currentLevelInstance;
    private int currentLevelIndex = -1;

    private void Start()
    {
        LoadLevel(0);
    }

    public void CompleteCurrentLevel()
    {
        if (levelDatabase == null || levelDatabase.levels == null)
        {
            Debug.LogError("Cannot load the next level because the LevelDatabase is missing.");
            return;
        }

        LoadLevel(currentLevelIndex + 1);
    }

    private void LoadLevel(int levelIndex)
    {
        if (levelDatabase == null)
        {
            Debug.LogError("LevelDatabase has not been assigned to GameManager.");
            return;
        }

        if (levelDatabase.levels == null || levelDatabase.levels.Count == 0)
        {
            Debug.LogError("LevelDatabase does not contain any levels.");
            return;
        }

        if (levelIndex < 0 || levelIndex >= levelDatabase.levels.Count)
        {
            Debug.Log("All levels have been completed.");
            return;
        }

        LevelData levelData = levelDatabase.levels[levelIndex];

        if (levelData == null || levelData.levelPrefab == null)
        {
            Debug.LogError($"Level at index {levelIndex} does not have a valid prefab.");
            return;
        }

        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
        }

        currentLevelInstance = Instantiate(levelData.levelPrefab, this.gameObject.transform);
        currentLevelIndex = levelIndex;
    }
}
