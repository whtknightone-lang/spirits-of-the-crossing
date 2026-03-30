import random


class Star:
    def __init__(self):
        self.base_energy = 3.0
        self.flare_prob = 0.005

    def emit_energy(self) -> float:
        flare = 0.0
        if random.random() < self.flare_prob:
            flare = random.uniform(3.0, 5.0)
        return self.base_energy + flare
