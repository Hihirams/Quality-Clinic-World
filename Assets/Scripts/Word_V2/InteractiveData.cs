using UnityEngine;

namespace Word_V2
{
    [System.Serializable]
    public class RegionData
    {
    public string regionName;
    public Color maskColor;
    public Texture2D backgroundTexture;
    public Texture2D maskTexture;
    public string nextSceneName; // Para el último nivel (estados)
}

[System.Serializable]
public class LevelData
{
    public string levelName;
    public Texture2D backgroundTexture;
    public Texture2D maskTexture;
    public RegionData[] regions;
}

    [CreateAssetMenu(fileName = "InteractiveData", menuName = "World/Interactive Data")]
    public class InteractiveData : ScriptableObject
    {
        public LevelData[] levels; // [0] = World, [1] = Continent, [2] = Country, [3] = State
    }
}
