import math


CARDINAL_ACTIONS = [
    ("up", (0, 1)),
    ("down", (0, -1)),
    ("left", (-1, 0)),
    ("right", (1, 0)),
]

DIAGONAL_ACTIONS = [
    ("up_left", (-1, 1)),
    ("up_right", (1, 1)),
    ("down_left", (-1, -1)),
    ("down_right", (1, -1)),
]


class GridEnv:
    def __init__(self, map_data, reward_config, allow_diagonal=False):
        self.map_data = map_data
        self.width = int(map_data["width"])
        self.height = int(map_data["height"])
        self.start = self._coord_to_tuple(map_data["start"])
        self.goal = self._coord_to_tuple(map_data["goal"])
        self.obstacles = {
            self._coord_to_tuple(item)
            for item in map_data.get("obstacles", [])
        }
        self.reward_config = reward_config
        self.actions = list(CARDINAL_ACTIONS)
        if allow_diagonal:
            self.actions.extend(DIAGONAL_ACTIONS)

    def reset(self):
        return self.start

    def step(self, state, action_index):
        action_name, delta = self.actions[action_index]
        next_state = (state[0] + delta[0], state[1] + delta[1])
        previous_distance = self.distance_to_goal(state)

        if not self.is_inside(next_state):
            return state, self.reward_config["out_of_bounds_penalty"], False, {
                "event": "out_of_bounds",
                "action": action_name,
            }

        if next_state in self.obstacles:
            return state, self.reward_config["obstacle_penalty"], False, {
                "event": "obstacle",
                "action": action_name,
            }

        if next_state == self.goal:
            return next_state, self.reward_config["goal_reward"], True, {
                "event": "goal",
                "action": action_name,
            }

        reward = self.reward_config["step_penalty"]
        current_distance = self.distance_to_goal(next_state)
        if current_distance < previous_distance:
            reward += self.reward_config["closer_reward"]
        elif current_distance > previous_distance:
            reward += self.reward_config["farther_penalty"]

        return next_state, reward, False, {
            "event": "move",
            "action": action_name,
        }

    def is_inside(self, state):
        return 0 <= state[0] < self.width and 0 <= state[1] < self.height

    def is_free(self, state):
        return self.is_inside(state) and state not in self.obstacles

    def distance_to_goal(self, state):
        return math.hypot(self.goal[0] - state[0], self.goal[1] - state[1])

    @staticmethod
    def _coord_to_tuple(coord):
        return int(coord["x"]), int(coord["y"])
