import random


class QLearningAgent:
    def __init__(
        self,
        action_count,
        learning_rate,
        discount_factor,
        epsilon,
        epsilon_decay,
        min_epsilon,
        random_seed=None,
    ):
        self.action_count = action_count
        self.learning_rate = learning_rate
        self.discount_factor = discount_factor
        self.epsilon = epsilon
        self.epsilon_decay = epsilon_decay
        self.min_epsilon = min_epsilon
        self.q_table = {}
        self.random = random.Random(random_seed)

    def choose_action(self, state):
        self._ensure_state(state)
        if self.random.random() < self.epsilon:
            return self.random.randrange(self.action_count)
        return self.best_action(state)

    def best_action(self, state):
        self._ensure_state(state)
        values = self.q_table[state]
        best_value = max(values)
        best_actions = [
            index
            for index, value in enumerate(values)
            if value == best_value
        ]
        return self.random.choice(best_actions)

    def update(self, state, action, reward, next_state, done):
        self._ensure_state(state)
        self._ensure_state(next_state)
        old_value = self.q_table[state][action]
        next_best = 0.0 if done else max(self.q_table[next_state])
        target = reward + self.discount_factor * next_best
        self.q_table[state][action] = old_value + self.learning_rate * (target - old_value)

    def decay_epsilon(self):
        self.epsilon = max(self.min_epsilon, self.epsilon * self.epsilon_decay)

    def greedy_path(self, env, max_steps):
        state = env.reset()
        path = [state]
        visited = {state}
        total_reward = 0.0

        for _ in range(max_steps):
            if state == env.goal:
                return path, True, total_reward

            action = self.best_action(state)
            next_state, reward, done, _ = env.step(state, action)
            total_reward += reward

            if next_state == state and not done:
                return path, False, total_reward

            path.append(next_state)
            if next_state in visited and next_state != env.goal:
                return path, False, total_reward

            visited.add(next_state)
            state = next_state
            if done:
                return path, True, total_reward

        return path, state == env.goal, total_reward

    def export_policy(self, env):
        policy = []
        for y in range(env.height):
            for x in range(env.width):
                state = (x, y)
                if state in env.obstacles:
                    continue
                self._ensure_state(state)
                action_index = self.best_action(state)
                action_name, _ = env.actions[action_index]
                policy.append({
                    "x": x,
                    "y": y,
                    "best_action": action_name,
                    "q_values": list(self.q_table[state]),
                })
        return policy

    def _ensure_state(self, state):
        if state not in self.q_table:
            self.q_table[state] = [0.0 for _ in range(self.action_count)]
