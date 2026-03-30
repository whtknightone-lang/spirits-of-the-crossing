import numpy as np

from engine.snowflake.brain import SnowflakeBrain3DFluid
from engine.lore.archetypes import get_archetype, default_drives


def _circular_diff(a: float, b: float) -> float:
    return float(np.angle(np.exp(1j * (a - b))))


class Agent:
    """
    An agent in the RUE universe.

    The agent's brain is a SnowflakeBrain3DFluid. Its 'signature' is the
    mean phase of all brain nodes, used for social matching and encounter
    resolution.

    Reward structure each step:
      + match with local field phase * local energy
      + social alignment bonus
      + identity stability (brain vs memory)
      - mismatch penalty
      - living cost
      - social repulsion penalty
      - coherence penalty (if brain sync drops below floor)
    """

    def __init__(self, planet_id: int, region_id: int, archetype: str = None):
        self.planet_id = planet_id
        self.region_id = region_id
        self.brain = SnowflakeBrain3DFluid()
        self.energy = 2.0
        self.last_reward = 0.0
        self.last_penalty = 0.0

        # spirit archetype (Nahual) — biases drive weights
        self.archetype = archetype or "FlowDancer"
        arc = get_archetype(self.archetype)
        self.drives = arc.drive_weights.copy() if arc else default_drives()

        # base constants modulated by archetype
        self.IDENTITY_GAIN = 0.22 + 0.10 * self.drives["seek"]
        self.COHERENCE_FLOOR = 0.20 + 0.20 * self.drives["rest"]
        self.COHERENCE_PENALTY = 0.08
        self.CORE_REINFORCEMENT_GAIN = 0.30 + 0.15 * self.drives["explore"]

        # last field perception (updated by step_with_field)
        self.last_field_alignment = 0.0

    def signature(self) -> float:
        """Mean phase of the brain — used as the agent's resonance identity."""
        return self.brain.mean_phase()

    def reward_components(
        self, local_phase: float, local_energy: float, social_match: float
    ):
        sig = self.signature()
        match = np.cos(_circular_diff(sig, local_phase))

        positive = max(0.0, match - 0.10) * local_energy
        mismatch = max(0.0, 0.15 - match) * 0.35 * local_energy
        living_cost = 0.05
        social_bonus = max(0.0, social_match) * 0.10
        social_penalty = max(0.0, -social_match) * 0.08

        identity = self.brain.identity_match() * self.IDENTITY_GAIN

        coherence = self.brain.sync()
        if coherence < self.COHERENCE_FLOOR:
            mismatch += self.COHERENCE_PENALTY

        reward = positive + social_bonus + identity
        penalty = mismatch + living_cost + social_penalty
        return reward, penalty, match

    def step(self, local_phase: float, local_energy: float, social_match: float):
        """Backwards-compatible step — no field input."""
        from engine.upsilon.field import FieldSample
        self.step_with_field(local_phase, local_energy, social_match, FieldSample())

    def step_with_field(
        self,
        local_phase: float,
        local_energy: float,
        social_match: float,
        field_sample,
    ):
        """
        Full step with Upsilon field perception.

        The FieldSample enriches both the brain drive and the reinforcement signal:
          - Field intensity * phase alignment adds to base_drive
          - Field coherence adds a small constant drive
          - Strong phase alignment with a coherent field boosts reinforcement
            (the agent is rewarded for resonating with the field)
        """
        reward, penalty, match = self.reward_components(
            local_phase, local_energy, social_match
        )

        # base drive from region + social
        # seek weight scales how strongly the agent reaches outward
        seek_scale = 0.5 + self.drives["seek"]
        base_drive = np.tanh(local_energy / 5.0) * 0.010 * seek_scale
        # signal weight amplifies social influence
        signal_scale = 0.5 + self.drives["signal"]
        base_drive += 0.010 * social_match * signal_scale
        base_drive -= 0.008 * max(0.0, -social_match)
        base_drive += 0.010 * match

        # Upsilon field contribution to drive
        # explore weight scales how much the agent tunes into the Upsilon field
        explore_scale = 0.5 + self.drives["explore"]
        base_drive += 0.008 * field_sample.intensity * field_sample.phase_alignment * explore_scale
        base_drive += 0.005 * field_sample.coherence * explore_scale

        # field alignment bonus to reinforcement (self-regulation signal)
        field_bonus = max(0.0, field_sample.phase_alignment) * 0.10 * field_sample.coherence
        reinforcement = (
            self.CORE_REINFORCEMENT_GAIN * max(0.0, reward - penalty) + field_bonus
        )

        self.brain.step(drive=base_drive, reinforcement=reinforcement)
        self.energy += reward - penalty + field_bonus * 0.05
        self.last_reward = reward + field_bonus * 0.05
        self.last_penalty = penalty
        self.last_field_alignment = field_sample.phase_alignment
