import csv
import json
from pathlib import Path


def write_path_json(output_path, map_data, path, success, path_length, episodes, total_reward, planning_time, message):
    result = {
        "algorithm": "Q-learning",
        "drone_id": map_data.get("drone_id", 0),
        "task_id": map_data.get("task_id", 0),
        "start": map_data.get("start", {"x": 0, "y": 0}),
        "goal": map_data.get("goal", {"x": 0, "y": 0}),
        "path": [{"x": int(x), "y": int(y)} for x, y in path],
        "success": bool(success),
        "path_length": float(path_length),
        "training_episodes": int(episodes),
        "total_reward": float(total_reward),
        "planning_time": float(planning_time),
        "message": message,
        "world_transform": map_data.get("world_transform", {}),
    }
    _write_json(output_path, result)


def write_policy_json(output_path, map_data, policy):
    result = {
        "algorithm": "Q-learning",
        "drone_id": map_data.get("drone_id", 0),
        "task_id": map_data.get("task_id", 0),
        "policy": policy,
    }
    _write_json(output_path, result)


def write_reward_log(output_path, rewards):
    Path(output_path).parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, "w", newline="", encoding="utf-8") as file:
        writer = csv.writer(file)
        writer.writerow(["episode", "total_reward"])
        for index, reward in enumerate(rewards, start=1):
            writer.writerow([index, f"{reward:.6f}"])


def _write_json(output_path, data):
    Path(output_path).parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as file:
        json.dump(data, file, ensure_ascii=False, indent=2)
