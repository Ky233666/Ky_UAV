using UnityEngine;

/// <summary>
/// Marks runtime-authored obstacles so they can be counted and deleted safely.
/// </summary>
public class RuntimeObstacleMarker : MonoBehaviour
{
    public int obstacleId;
    public string templateDisplayName = "长方体";
}
