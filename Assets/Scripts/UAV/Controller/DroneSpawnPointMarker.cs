using UnityEngine;

/// <summary>
/// Marks a manually placed drone spawn point in the scene.
/// </summary>
public class DroneSpawnPointMarker : MonoBehaviour
{
    [Tooltip("Used to keep manually placed spawn points in click order.")]
    public int orderIndex;
}
