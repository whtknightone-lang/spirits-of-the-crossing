import numpy as np


class UpsilonNode:
    """
    An Upsilon Vibration Node — a time-varying signal source in 2D world space.

    Each node emits a sinusoidal field that attenuates with distance.
    Agents within influence_radius can sense the signal and, through
    circular causality, feed back into the node's amplitude and phase.

    Orbit parameters (planet_id, orbit_r, orbit_offset) let the Universe
    keep the node tethered to a planet without the node needing to know
    about the planet directly.
    """

    def __init__(
        self,
        world_pos,
        frequency: float,
        amplitude: float = 1.0,
        phase: float = None,
        influence_radius: float = 3.5,
        planet_id: int = None,
        orbit_r: float = 0.8,
        orbit_offset: float = 0.0,
    ):
        self.pos = np.array(world_pos, dtype=float)
        self.frequency = frequency
        self.amplitude = amplitude
        self.phase = phase if phase is not None else np.random.rand() * 2 * np.pi
        self.influence_radius = influence_radius

        # orbit bookkeeping (managed externally by Universe)
        self.planet_id = planet_id
        self.orbit_r = orbit_r
        self.orbit_offset = orbit_offset

    # ------------------------------------------------------------------
    # Dynamics
    # ------------------------------------------------------------------

    def step(self, dt: float = 0.01) -> None:
        """Advance the node's phase by one time step."""
        self.phase = np.mod(self.phase + 2 * np.pi * self.frequency * dt, 2 * np.pi)

    def signal_at(self, distance: float) -> float:
        """
        Instantaneous signal value at a given distance.
        S(d) = A * sin(φ) / (1 + d²),  0 outside influence_radius.
        """
        if distance > self.influence_radius:
            return 0.0
        attenuation = 1.0 / (1.0 + distance ** 2)
        return self.amplitude * np.sin(self.phase) * attenuation

    # ------------------------------------------------------------------
    # Circular causality — agents affect the node
    # ------------------------------------------------------------------

    def receive_feedback(
        self, agent_phase: float, agent_energy: float, agent_sync: float
    ) -> None:
        """
        Agents that are energetic or coherent leave an imprint on the node.

        - High energy  → small amplitude boost (agent pumps the field)
        - High sync    → node phase nudged toward agent's brain phase
                         (mutual entrainment; the field learns the agent)
        """
        if agent_energy > 2.5:
            self.amplitude = float(np.clip(self.amplitude * 1.001, 0.1, 2.5))

        if agent_sync > 0.65:
            diff = float(np.angle(np.exp(1j * (agent_phase - self.phase))))
            self.phase = np.mod(self.phase + 0.005 * diff, 2 * np.pi)

        # slow amplitude decay back toward 1.0 to prevent runaway
        self.amplitude += 0.0002 * (1.0 - self.amplitude)
