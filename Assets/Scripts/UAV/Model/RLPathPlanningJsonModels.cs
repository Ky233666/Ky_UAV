using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RLGridCoord
{
    public int x;
    public int y;

    public RLGridCoord()
    {
    }

    public RLGridCoord(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(x, y);
    }
}

[Serializable]
public class RLWorldTransform
{
    public float origin_x;
    public float origin_z;
    public float cell_size = 1f;
    public float cruise_y = 5f;
}

[Serializable]
public class RLTaskPointJson
{
    public int task_id;
    public int x;
    public int y;
    public int priority;
}

[Serializable]
public class RLMapJson
{
    public int width;
    public int height;
    public float grid_size = 1f;
    public int drone_id;
    public int task_id;
    public RLGridCoord start = new RLGridCoord();
    public RLGridCoord goal = new RLGridCoord();
    public List<RLGridCoord> obstacles = new List<RLGridCoord>();
    public List<RLTaskPointJson> task_points = new List<RLTaskPointJson>();
    public RLWorldTransform world_transform = new RLWorldTransform();
}

[Serializable]
public class RLPathJson
{
    public string algorithm = "Q-learning";
    public int drone_id;
    public int task_id;
    public List<RLGridCoord> path = new List<RLGridCoord>();
    public bool success;
    public float path_length;
    public int training_episodes;
    public float total_reward;
    public float planning_time;
    public RLGridCoord start = new RLGridCoord();
    public RLGridCoord goal = new RLGridCoord();
    public string message = "";
    public RLWorldTransform world_transform = new RLWorldTransform();
}
