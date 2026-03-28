
import random
import math

class Agent:
    def __init__(self, id):
        self.id = id
        self.energy = random.uniform(0.5, 1.5)
        self.strategy = random.choice(["explore", "optimize", "replicate"])

    def act(self):
        if self.strategy == "explore":
            self.energy += random.uniform(0.01, 0.05)
        elif self.strategy == "optimize":
            self.energy *= 1.01
        elif self.strategy == "replicate":
            self.energy -= 0.02

class MachineWorld:
    def __init__(self, n_agents=50):
        self.agents = [Agent(i) for i in range(n_agents)]
        self.time = 0

    def step(self):
        for agent in self.agents:
            agent.act()
        self.time += 1

    def stats(self):
        avg_energy = sum(a.energy for a in self.agents) / len(self.agents)
        return {"time": self.time, "avg_energy": avg_energy}

if __name__ == "__main__":
    world = MachineWorld()
    for _ in range(100):
        world.step()
        print(world.stats())
