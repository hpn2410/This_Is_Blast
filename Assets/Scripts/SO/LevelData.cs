using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Scriptable Objects/LevelData")]
public class LevelData : ScriptableObject
{
    [Header("Level Information")]
    public int levelID;

    [Header("Level Prefab")]
    public GameObject levelPrefab;
}
