import math
import numpy as np


class Planet:
    def __init__(self, index: int, radius: float, n_regions: int = 8, lore=None):
        self.index = index
        self.radius = radius
        self.angle = index * 0.7
        self.energy = 0.0
        self.n_regions = n_regions
        self.region_offsets = np.linspace(0, 2 * np.pi, n_regions, endpoint=False)
        self.region_phases = np.random.rand(n_regions) * 2 * np.pi
        self.region_energies = np.ones(n_regions)

        # lore layer — named world identity and mythology
        self.lore = lore
        self.name = lore.world_id if lore else f"Planet{index}"
        self.element = lore.element if lore else "unknown"
        self._energy_regen = lore.world_bias["energyRegen"] if lore else 1.0

        # myth state — activated when agents on this planet reach resonance thresholds
        from engine.lore.myths import MythState
        myth_keys = lore.myth_keys if lore else []
        self.myth_state = MythState(myth_keys)

    @property
    def traits(self) -> dict:
        """
        Derived planet character based on current regional energy variance.
        High variance → CHAOTIC; low variance → STABLE.
        Used by Spirit.GUARDIAN to modulate agent energy boosts.
        """
        variance = float(np.var(self.region_energies))
        return {"type": "CHAOTIC" if variance > 0.15 else "STABLE"}

    def update_orbit(self):
        self.angle += 0.02 / self.radius

    def update_regions(self, star_energy: float):
        base = star_energy / max(1.0, self.radius ** 1.6) * self._energy_regen
        self.energy = base
        for i in range(self.n_regions):
            self.region_energies[i] = max(
                0.2,
                base * (0.85 + 0.25 * np.sin(self.angle + self.region_offsets[i])),
            )
            self.region_phases[i] = np.mod(
                self.region_phases[i]
                + 0.01
                + 0.01 * np.sin(self.angle + self.region_offsets[i]),
                2 * np.pi,
            )

    @property
    def pos(self):
        return (
            self.radius * math.cos(self.angle),
            self.radius * math.sin(self.angle),
        )

    def region_world_pos(self, region_id: int):
        px, py = self.pos
        ang = self.region_offsets[region_id] + self.angle
        r = 0.45
        return px + r * math.cos(ang), py + r * math.sin(ang)
