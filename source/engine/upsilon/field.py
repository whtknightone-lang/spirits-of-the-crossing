from dataclasses import dataclass, field as dc_field
import numpy as np


@dataclass
class FieldSample:
    """
    The agent's perception of the Upsilon field at its current position.

    intensity        — net signal strength (signed sum of all node contributions)
    phase_alignment  — cos(agent_phase - field_phase): +1 = fully aligned, -1 = opposed
    dominant_frequency — amplitude-weighted mean node frequency in range
    coherence        — Kuramoto-style order of node phases within range [0, 1]
    gradient         — 2D unit-ish vector pointing toward the net field source
    """

    intensity: float = 0.0
    phase_alignment: float = 0.0
    dominant_frequency: float = 0.0
    coherence: float = 0.0
    gradient: np.ndarray = dc_field(default_factory=lambda: np.zeros(2))


class NodeField:
    """
    Aggregates all UpsilonNodes into a continuous field.
    Provides a single sample() call that returns a FieldSample for any
    2D world position and agent brain phase.
    """

    def __init__(self, nodes):
        self.nodes = nodes

    def sample(self, world_pos, agent_phase: float) -> FieldSample:
        """
        Direct summation propagation model (§5.2.1 of Upsilon spec).
        Efficiently handles the small-to-medium node counts typical here.
        """
        pos = np.asarray(world_pos, dtype=float)

        total_signal = 0.0
        total_weight = 0.0
        weighted_phase = 0j      # complex accumulator for circular mean
        weighted_freq = 0.0
        gradient = np.zeros(2)

        for node in self.nodes:
            d = float(np.linalg.norm(pos - node.pos))
            if d > node.influence_radius:
                continue

            sig = node.signal_at(d)
            w = node.amplitude / (1.0 + d ** 2)   # amplitude-weighted proximity

            total_signal += sig
            total_weight += w
            weighted_phase += w * np.exp(1j * node.phase)
            weighted_freq += node.frequency * w

            # gradient: direction from agent toward node, weighted by strength
            if d > 1e-6:
                gradient += (node.pos - pos) / d * w

        if total_weight < 1e-9:
            return FieldSample()   # zero sample — no nodes in range

        field_phase = float(np.angle(weighted_phase))
        coherence = float(abs(weighted_phase) / total_weight)
        phase_alignment = float(np.cos(agent_phase - field_phase))
        dominant_frequency = weighted_freq / total_weight

        # normalise gradient to unit length (keep zero if no nodes in range)
        grad_norm = float(np.linalg.norm(gradient))
        if grad_norm > 1e-6:
            gradient = gradient / grad_norm

        return FieldSample(
            intensity=total_signal,
            phase_alignment=phase_alignment,
            dominant_frequency=dominant_frequency,
            coherence=coherence,
            gradient=gradient,
        )
