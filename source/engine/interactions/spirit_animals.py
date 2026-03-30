import numpy as np


class SpiritAnimal:
    """
    A field-level regulatory entity that operates on the whole universe each tick.

    Types:
      BALANCER   — corrects energy outliers; pulls agents back toward the group mean
      CATALYST   — finds the lowest-sync agent on each planet and nudges its brain
      STABILIZER — applies a gentle mean-reversion force to all agent energies
    """

    def __init__(self, kind: str):
        self.kind = kind

    def act(self, universe) -> None:
        if self.kind == "BALANCER":
            self._balance(universe)
        elif self.kind == "CATALYST":
            self._catalyse(universe)
        elif self.kind == "STABILIZER":
            self._stabilise(universe)

    # ------------------------------------------------------------------

    def _balance(self, universe) -> None:
        """Pull agents that have drifted ±2.0 energy back toward the mean."""
        energies = [a.energy for a in universe.agents]
        mean_e = float(np.mean(energies))
        for a in universe.agents:
            if a.energy > mean_e + 2.0:
                a.energy -= 0.05
            elif a.energy < mean_e - 2.0:
                a.energy += 0.05

    def _catalyse(self, universe) -> None:
        """Give each planet's least-coherent agent an extra brain drive tick."""
        for p in universe.planets:
            planet_agents = [a for a in universe.agents if a.planet_id == p.index]
            if not planet_agents:
                continue
            lowest = min(planet_agents, key=lambda a: a.brain.sync())
            lowest.brain.step(drive=0.02)

    def _stabilise(self, universe) -> None:
        """Soft mean-reversion: nudge every agent's energy 1 % toward the mean."""
        energies = [a.energy for a in universe.agents]
        mean_e = float(np.mean(energies))
        for a in universe.agents:
            a.energy += 0.01 * (mean_e - a.energy)
