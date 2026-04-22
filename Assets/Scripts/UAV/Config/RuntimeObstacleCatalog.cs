using UnityEngine;

/// <summary>
/// Runtime obstacle template catalog used by the obstacle editor.
/// </summary>
[CreateAssetMenu(fileName = "RuntimeObstacleCatalog", menuName = "KY UAV/Runtime Obstacle Catalog")]
public class RuntimeObstacleCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string displayName = "城市楼体";
        public GameObject prefab;
        public bool preserveAspect = true;
    }

    [Tooltip("可在运行时选择的障碍物模板。")]
    public Entry[] entries = new Entry[0];
}
