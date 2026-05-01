import argparse
import json
import math
import time
from pathlib import Path

import config
from export_result import write_path_json, write_policy_json, write_reward_log
from grid_env import GridEnv
from qlearning_agent import QLearningAgent


def main():
    parser = argparse.ArgumentParser(description="Train Q-learning path planning on a Unity-exported map.json.")
    parser.add_argument("--case", default=None, help="Case name or case directory under cases/.")
    parser.add_argument("--map", default=None, help="Input map JSON path. Defaults to <case>/map.json.")
    parser.add_argument("--output-dir", default=None, help="Output directory. Defaults to the selected case directory.")
    parser.add_argument("--episodes", type=int, default=config.EPISODES, help="Training episode count.")
    parser.add_argument("--allow-diagonal", action="store_true", default=config.ALLOW_DIAGONAL, help="Enable 8-direction movement.")
    args = parser.parse_args()

    module_dir = Path(__file__).resolve().parent
    case_dir = _resolve_case_dir(module_dir, args.case)
    if args.map:
        map_path = _resolve_path(module_dir, args.map)
    elif case_dir is not None:
        map_path = case_dir / "map.json"
    else:
        map_path = module_dir / "input" / "map.json"

    if args.output_dir:
        output_dir = _resolve_path(module_dir, args.output_dir)
    elif case_dir is not None:
        output_dir = case_dir
    elif args.map:
        output_dir = map_path.parent
    else:
        output_dir = module_dir / "output"

    output_dir.mkdir(parents=True, exist_ok=True)

    with open(map_path, "r", encoding="utf-8-sig") as file:
        map_data = json.load(file)

    reward_config = {
        "goal_reward": config.GOAL_REWARD,
        "obstacle_penalty": config.OBSTACLE_PENALTY,
        "out_of_bounds_penalty": config.OUT_OF_BOUNDS_PENALTY,
        "step_penalty": config.STEP_PENALTY,
        "closer_reward": config.CLOSER_REWARD,
        "farther_penalty": config.FARTHER_PENALTY,
    }
    env = GridEnv(map_data, reward_config, allow_diagonal=args.allow_diagonal)
    _validate_map(env)

    max_steps = config.MAX_STEPS_PER_EPISODE
    if max_steps is None:
        max_steps = max(1, env.width * env.height)

    agent = QLearningAgent(
        action_count=len(env.actions),
        learning_rate=config.LEARNING_RATE,
        discount_factor=config.DISCOUNT_FACTOR,
        epsilon=config.EPSILON,
        epsilon_decay=config.EPSILON_DECAY,
        min_epsilon=config.MIN_EPSILON,
        random_seed=config.RANDOM_SEED,
    )

    rewards = []
    started_at = time.perf_counter()
    for _ in range(args.episodes):
        state = env.reset()
        episode_reward = 0.0
        for _ in range(max_steps):
            action = agent.choose_action(state)
            next_state, reward, done, _ = env.step(state, action)
            agent.update(state, action, reward, next_state, done)
            episode_reward += reward
            state = next_state
            if done:
                break
        agent.decay_epsilon()
        rewards.append(episode_reward)

    path, success, rollout_reward = agent.greedy_path(env, max_steps)
    planning_time = time.perf_counter() - started_at
    path_length = _calculate_path_length(path)
    message = "Goal reached by greedy rollout from learned Q-table." if success else "Training finished, but greedy rollout did not reach goal."

    write_path_json(
        output_dir / "path.json",
        map_data,
        path,
        success,
        path_length,
        args.episodes,
        rollout_reward,
        planning_time,
        message,
    )
    write_policy_json(output_dir / "policy.json", map_data, agent.export_policy(env))
    write_reward_log(output_dir / "reward_log.csv", rewards)

    drone_id = int(map_data.get("drone_id", 0))
    task_id = int(map_data.get("task_id", 0))
    if drone_id > 0:
        named_path = output_dir / f"path_drone_{drone_id:02d}.json"
        write_path_json(named_path, map_data, path, success, path_length, args.episodes, rollout_reward, planning_time, message)
    if drone_id > 0 and task_id > 0:
        named_task_path = output_dir / f"path_drone_{drone_id:02d}_task_{task_id:03d}.json"
        write_path_json(named_task_path, map_data, path, success, path_length, args.episodes, rollout_reward, planning_time, message)

    print(f"success={success} path_length={path_length:.3f} episodes={args.episodes} time={planning_time:.3f}s")
    print(f"wrote {output_dir / 'path.json'}")


def _resolve_path(module_dir, value):
    path = Path(value)
    if path.is_absolute():
        return path
    return module_dir / path


def _resolve_case_dir(module_dir, case_value):
    cases_dir = module_dir / "cases"
    if case_value:
        case_path = Path(case_value)
        if case_path.is_absolute():
            return case_path
        if len(case_path.parts) > 1:
            return module_dir / case_path
        return cases_dir / case_value

    latest = _find_latest_case_dir(cases_dir)
    return latest


def _find_latest_case_dir(cases_dir):
    if not cases_dir.exists():
        return None

    candidates = [
        path
        for path in cases_dir.iterdir()
        if path.is_dir() and (path / "map.json").exists()
    ]
    if not candidates:
        return None

    candidates.sort(key=lambda path: path.stat().st_mtime, reverse=True)
    return candidates[0]


def _validate_map(env):
    if not env.is_free(env.start):
        raise ValueError(f"start cell is blocked or out of bounds: {env.start}")
    if not env.is_free(env.goal):
        raise ValueError(f"goal cell is blocked or out of bounds: {env.goal}")


def _calculate_path_length(path):
    total = 0.0
    for index in range(1, len(path)):
        dx = path[index][0] - path[index - 1][0]
        dy = path[index][1] - path[index - 1][1]
        total += math.hypot(dx, dy)
    return total


if __name__ == "__main__":
    main()
