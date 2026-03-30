import random


class Spirit:
    """
    A named spiritual presence that acts on agents each tick.

    Levels:
      MINOR    — boosts a random single agent universe-wide
      MAJOR    — boosts all agents on its assigned planet
      GUARDIAN — boosts agents on its planet; boost size depends on planet.traits
    """

    def __init__(self, level: str, role: str, planet_id: int = None):
        self.level = level
        self.role = role
        self.planet_id = planet_id

    def act(self, universe) -> None:
        if self.level == "MINOR":
            a = random.choice(universe.agents)
            a.energy += 0.01

        elif self.level == "MAJOR":
            for a in universe.agents:
                if a.planet_id == self.planet_id:
                    a.energy += 0.005

        elif self.level == "GUARDIAN":
            planet = universe.planets[self.planet_id]
            if planet.traits["type"] == "CHAOTIC":
                # stronger presence in turbulent environments
                for a in universe.agents:
                    if a.planet_id == self.planet_id:
                        a.energy += 0.01
            else:  # STABLE
                for a in universe.agents:
                    if a.planet_id == self.planet_id:
                        a.energy += 0.002


def create_spirits(num_planets: int):
    """
    Produce a standard spirit roster:
      - one GUARDIAN + one MAJOR per planet
      - five roaming MINOR spirits
    """
    spirits = []
    for i in range(num_planets):
        spirits.append(Spirit("GUARDIAN", "PLANET", planet_id=i))
        spirits.append(Spirit("MAJOR", "INFLUENCE", planet_id=i))
    for _ in range(5):
        spirits.append(Spirit("MINOR", "LOCAL"))
    return spirits
