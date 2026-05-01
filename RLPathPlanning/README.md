# RLPathPlanning

Offline Q-learning path planning module for the Unity UAV simulation project.

## Unity workflow

Recommended for normal demos:

1. In Unity Play mode, enter the RL training scene and create or edit obstacles.
2. Click `导出地图`; Unity writes a case directory like:

```text
cases/map_drone_01_task_001_20260502_153000/map.json
```

3. Click `训练并显示`; Unity starts the Python trainer in the background with the selected case.
4. When training finishes, Unity imports the same case's `path.json`, draws the RL path, and shows the exported map obstacles.
5. Click `开始` in `RL运行` to let the drone follow the imported RL path.

Unity resolves Python from:

```text
RLPathPlanning/.venv/Scripts/python.exe
```

If a different interpreter is needed, set `pythonExecutablePath` on `RLQlearningTrainingRunner`.

## Command-line fallback

1. Export a case directory from Unity, for example:

```text
cases/map_drone_01_task_001_20260502_153000/map.json
```

2. Run:

```powershell
python .\train_qlearning.py --case map_drone_01_task_001_20260502_153000
```

If `--case` is omitted, the trainer uses the newest directory under `cases/` that contains `map.json`.

3. The trainer writes results into the same case directory:

- `cases/<case>/path.json`
- `cases/<case>/policy.json`
- `cases/<case>/reward_log.csv`

4. Select the case in Unity with the RL case selector, then import/show it from the Unity panel.

## Notes

- This module implements tabular Q-learning only.
- It is a single-agent grid path planner.
- It is not DQN, PPO, Unity ML-Agents, or multi-agent reinforcement learning.
- Unity can start Python automatically through `RLQlearningTrainingRunner` during Play mode.
- The command-line trainer remains available for debugging and repeatable experiments.
- If the Unity map changes, export `map.json` again and retrain.
