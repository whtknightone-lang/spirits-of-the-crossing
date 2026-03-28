"""
Rainbow Resonance Processor v2
==============================

A compact research prototype for the "Rainbow Resonance Processor" concept.

What this prototype includes
----------------------------
- 7 spectral bands (red -> violet)
- 2D lattice of resonance nodes
- Per-node phase and amplitude dynamics
- Local spatial coupling within each band
- Cross-spectral coupling between bands
- Hebbian-like memory trace updates
- Global coherence measurement
- Slow "dream / drift" retuning mode
- Simple symbolic readout from dominant band energies

This is not intended as a biologically or physically exact model.
It is an experimental oscillator-field simulator that makes the earlier
architecture concrete enough to explore computational behavior.

Author: OpenAI / ChatGPT
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, List, Tuple, Optional
import math
import numpy as np


# -----------------------------------------------------------------------------
# Spectral definitions
# -----------------------------------------------------------------------------

BAND_NAMES = ["red", "orange", "yellow", "green", "blue", "indigo", "violet"]
NUM_BANDS = len(BAND_NAMES)
BAND_INDEX = {name: i for i, name in enumerate(BAND_NAMES)}

# Tone-like relative frequencies (not strict musical tuning, but ordered ratios).
# These act as intrinsic oscillator frequencies for the seven bands.
BASE_TONE_HZ = np.array([261.63, 293.66, 329.63, 349.23, 392.00, 440.00, 493.88], dtype=np.float64)

# Approximate visible-light center wavelengths in nm (used symbolically here).
WAVELENGTH_NM = np.array([680.0, 620.0, 580.0, 530.0, 470.0, 430.0, 400.0], dtype=np.float64)


# -----------------------------------------------------------------------------
# Utility functions
# -----------------------------------------------------------------------------

def clip01(x: np.ndarray) -> np.ndarray:
    return np.clip(x, 0.0, 1.0)


def toroidal_roll_sum(x: np.ndarray) -> np.ndarray:
    """
    4-neighborhood periodic (toroidal) Laplacian-style neighbor sum.
    """
    return (
        np.roll(x, 1, axis=0)
        + np.roll(x, -1, axis=0)
        + np.roll(x, 1, axis=1)
        + np.roll(x, -1, axis=1)
    )


def normalize_rows(m: np.ndarray, eps: float = 1e-8) -> np.ndarray:
    row_sums = np.sum(np.abs(m), axis=1, keepdims=True) + eps
    return m / row_sums


# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------

@dataclass
class RRPv2Config:
    width: int = 32
    height: int = 32
    dt: float = 0.02
    seed: int = 7

    # Phase dynamics
    phase_coupling_local: float = 0.22
    phase_coupling_cross: float = 0.18
    input_phase_gain: float = 0.35

    # Amplitude dynamics: dA/dt = alpha*A - beta*A^3 + coupling + input - decay
    amplitude_alpha: float = 0.90
    amplitude_beta: float = 1.00
    amplitude_neighbor_gain: float = 0.08
    amplitude_cross_gain: float = 0.08
    amplitude_input_gain: float = 0.75
    amplitude_decay: float = 0.06

    # Memory / plasticity
    memory_lr: float = 0.0015
    memory_decay: float = 0.0002
    memory_strength: float = 0.25

    # Dream / drift mode
    drift_frequency_noise: float = 0.004
    drift_cross_coupling_noise: float = 0.002
    drift_amplitude_noise: float = 0.01

    # Readout and stability
    coherence_window: int = 10
    max_input_amplitude: float = 1.0

    # Base frequency scaling into simulation units.
    # We normalize frequencies so the simulator is numerically stable.
    frequency_scale: float = 2.0 * math.pi / 500.0


# -----------------------------------------------------------------------------
# Input encoding
# -----------------------------------------------------------------------------

class SpectralEncoder:
    """
    Converts scalar/vector patterns into 7-band spectral injection fields.

    Two useful modes are included:
    1) encode_patch: inject localized activity centered on a band and position.
    2) encode_vector: inject a 7D vector directly across the whole lattice.
    """

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height

    def gaussian_patch(
        self,
        cx: float,
        cy: float,
        sigma: float,
        amplitude: float,
    ) -> np.ndarray:
        yy, xx = np.mgrid[0:self.height, 0:self.width]
        # Note: x aligns with width, y with height.
        d2 = (xx - cx) ** 2 + (yy - cy) ** 2
        patch = amplitude * np.exp(-d2 / (2.0 * sigma * sigma))
        return patch.astype(np.float64)

    def encode_patch(
        self,
        band: int,
        cx: float,
        cy: float,
        sigma: float = 3.0,
        amplitude: float = 1.0,
        spread_to_neighbors: float = 0.25,
    ) -> np.ndarray:
        fields = np.zeros((NUM_BANDS, self.height, self.width), dtype=np.float64)
        fields[band] += self.gaussian_patch(cx, cy, sigma, amplitude)

        # Bleed some energy to adjacent bands for harmonic richness.
        if band - 1 >= 0:
            fields[band - 1] += self.gaussian_patch(cx, cy, sigma * 1.15, amplitude * spread_to_neighbors)
        if band + 1 < NUM_BANDS:
            fields[band + 1] += self.gaussian_patch(cx, cy, sigma * 1.15, amplitude * spread_to_neighbors)

        return clip01(fields)

    def encode_vector(self, v: np.ndarray, amplitude: float = 0.35) -> np.ndarray:
        v = np.asarray(v, dtype=np.float64).reshape(NUM_BANDS)
        v = np.maximum(v, 0.0)
        if np.sum(v) > 0:
            v = v / (np.max(v) + 1e-8)
        fields = np.zeros((NUM_BANDS, self.height, self.width), dtype=np.float64)
        for b in range(NUM_BANDS):
            fields[b, :, :] = amplitude * v[b]
        return clip01(fields)

    def encode_symbol(self, symbol: str) -> np.ndarray:
        """
        Tiny symbolic encoding demo. Maps simple concepts to band emphasis.
        """
        symbol = symbol.strip().lower()
        presets: Dict[str, np.ndarray] = {
            "ground": np.array([1.0, 0.4, 0.2, 0.3, 0.1, 0.0, 0.0]),
            "flow": np.array([0.1, 0.3, 0.4, 0.6, 1.0, 0.5, 0.1]),
            "balance": np.array([0.2, 0.3, 0.5, 1.0, 0.5, 0.3, 0.2]),
            "insight": np.array([0.0, 0.1, 0.2, 0.3, 0.5, 0.9, 1.0]),
            "object": np.array([0.6, 0.5, 0.8, 0.6, 0.4, 0.2, 0.1]),
            "communication": np.array([0.1, 0.2, 0.4, 0.5, 1.0, 0.7, 0.3]),
        }
        v = presets.get(symbol, np.ones(NUM_BANDS, dtype=np.float64) * 0.15)
        return self.encode_vector(v)


# -----------------------------------------------------------------------------
# Processor core
# -----------------------------------------------------------------------------

@dataclass
class ProcessorState:
    phase: np.ndarray
    amplitude: np.ndarray
    omega: np.ndarray
    input_field: np.ndarray
    memory_trace: np.ndarray
    coherence_history: List[float] = field(default_factory=list)
    step_count: int = 0


class RainbowResonanceProcessorV2:
    def __init__(self, config: Optional[RRPv2Config] = None):
        self.cfg = config or RRPv2Config()
        self.rng = np.random.default_rng(self.cfg.seed)
        self.encoder = SpectralEncoder(self.cfg.width, self.cfg.height)

        h, w = self.cfg.height, self.cfg.width

        # Intrinsic frequencies per band, broadcast over the lattice.
        omega_base = self.cfg.frequency_scale * BASE_TONE_HZ
        omega = np.zeros((NUM_BANDS, h, w), dtype=np.float64)
        for b in range(NUM_BANDS):
            omega[b, :, :] = omega_base[b]
        omega += self.rng.normal(0.0, 0.01, size=omega.shape)

        phase = self.rng.uniform(0.0, 2.0 * math.pi, size=(NUM_BANDS, h, w))
        amplitude = clip01(0.10 + 0.04 * self.rng.standard_normal(size=(NUM_BANDS, h, w)))
        input_field = np.zeros((NUM_BANDS, h, w), dtype=np.float64)

        # Memory trace is a compact 7x7 cross-band plasticity matrix.
        memory_trace = np.eye(NUM_BANDS, dtype=np.float64) * 0.2

        # Cross-spectral coupling prior.
        self.cross_coupling = self._make_default_cross_coupling()

        self.state = ProcessorState(
            phase=phase,
            amplitude=amplitude,
            omega=omega,
            input_field=input_field,
            memory_trace=memory_trace,
        )

    def _make_default_cross_coupling(self) -> np.ndarray:
        """
        Hand-crafted structure for v2:
        - stronger adjacent-band coupling
        - green acts as a balancing hub
        - violet has moderate global integrative influence
        """
        c = np.zeros((NUM_BANDS, NUM_BANDS), dtype=np.float64)
        for i in range(NUM_BANDS):
            for j in range(NUM_BANDS):
                dist = abs(i - j)
                c[i, j] = math.exp(-0.9 * dist)

        green = BAND_INDEX["green"]
        violet = BAND_INDEX["violet"]
        blue = BAND_INDEX["blue"]
        indigo = BAND_INDEX["indigo"]
        red = BAND_INDEX["red"]

        c[green, :] += 0.20
        c[:, green] += 0.20
        c[violet, :] += 0.10
        c[:, violet] += 0.06
        c[blue, indigo] += 0.18
        c[indigo, blue] += 0.18
        c[red, green] += 0.14
        c[green, red] += 0.14

        np.fill_diagonal(c, np.diag(c) + 0.35)
        c = normalize_rows(c)
        return c

    # -------------------------------------------------------------------------
    # Input control
    # -------------------------------------------------------------------------

    def clear_input(self) -> None:
        self.state.input_field.fill(0.0)

    def set_input_field(self, field: np.ndarray) -> None:
        field = np.asarray(field, dtype=np.float64)
        if field.shape != self.state.input_field.shape:
            raise ValueError(f"Expected input field shape {self.state.input_field.shape}, got {field.shape}")
        self.state.input_field = clip01(field) * self.cfg.max_input_amplitude

    def inject_patch(
        self,
        band_name: str,
        cx: float,
        cy: float,
        sigma: float = 3.0,
        amplitude: float = 1.0,
        spread_to_neighbors: float = 0.25,
    ) -> None:
        band = BAND_INDEX[band_name.lower()]
        field = self.encoder.encode_patch(band, cx, cy, sigma, amplitude, spread_to_neighbors)
        self.set_input_field(field)

    def inject_symbol(self, symbol: str) -> None:
        self.set_input_field(self.encoder.encode_symbol(symbol))

    # -------------------------------------------------------------------------
    # Measurements
    # -------------------------------------------------------------------------

    def band_energy(self) -> np.ndarray:
        return np.mean(self.state.amplitude, axis=(1, 2))

    def band_phase_vector(self) -> np.ndarray:
        z = self.state.amplitude * np.exp(1j * self.state.phase)
        return np.mean(z, axis=(1, 2))

    def global_coherence(self) -> float:
        z = self.state.amplitude * np.exp(1j * self.state.phase)
        return float(np.abs(np.mean(z)))

    def per_band_coherence(self) -> np.ndarray:
        z = self.state.amplitude * np.exp(1j * self.state.phase)
        return np.abs(np.mean(z, axis=(1, 2)))

    def dominant_band(self) -> Tuple[str, float]:
        e = self.band_energy()
        idx = int(np.argmax(e))
        return BAND_NAMES[idx], float(e[idx])

    def symbolic_readout(self) -> Dict[str, object]:
        energies = self.band_energy()
        coherence = self.per_band_coherence()
        idx_energy = int(np.argmax(energies))
        idx_coherence = int(np.argmax(coherence))

        return {
            "dominant_energy_band": BAND_NAMES[idx_energy],
            "dominant_energy": float(energies[idx_energy]),
            "dominant_coherence_band": BAND_NAMES[idx_coherence],
            "dominant_coherence": float(coherence[idx_coherence]),
            "global_coherence": self.global_coherence(),
            "band_energies": {BAND_NAMES[i]: float(energies[i]) for i in range(NUM_BANDS)},
            "band_coherences": {BAND_NAMES[i]: float(coherence[i]) for i in range(NUM_BANDS)},
        }

    # -------------------------------------------------------------------------
    # Core dynamics
    # -------------------------------------------------------------------------

    def _cross_band_weight_matrix(self) -> np.ndarray:
        # Learned memory modifies the base cross-spectral matrix.
        learned = self.cfg.memory_strength * self.state.memory_trace
        out = self.cross_coupling + learned
        return normalize_rows(out)

    def _update_memory(self) -> None:
        """
        Compact Hebbian-style update on 7x7 band memory.
        If two bands are strong and phase-aligned, strengthen their connection.
        """
        band_z = self.band_phase_vector()
        amp = np.abs(band_z)
        ang = np.angle(band_z)

        hebb = np.zeros((NUM_BANDS, NUM_BANDS), dtype=np.float64)
        for i in range(NUM_BANDS):
            for j in range(NUM_BANDS):
                hebb[i, j] = amp[i] * amp[j] * math.cos(float(ang[i] - ang[j]))

        self.state.memory_trace *= (1.0 - self.cfg.memory_decay)
        self.state.memory_trace += self.cfg.memory_lr * hebb
        self.state.memory_trace = np.clip(self.state.memory_trace, -1.0, 1.0)

    def step(self, dream_mode: bool = False) -> Dict[str, object]:
        s = self.state
        cfg = self.cfg
        dt = cfg.dt

        # Optional dream/drift retuning.
        if dream_mode:
            s.omega += self.rng.normal(0.0, cfg.drift_frequency_noise, size=s.omega.shape)
            self.cross_coupling += self.rng.normal(0.0, cfg.drift_cross_coupling_noise, size=self.cross_coupling.shape)
            self.cross_coupling = normalize_rows(self.cross_coupling)
            s.amplitude += self.rng.normal(0.0, cfg.drift_amplitude_noise, size=s.amplitude.shape)
            s.amplitude = clip01(s.amplitude)

        cross_w = self._cross_band_weight_matrix()

        # Local within-band neighbor interactions.
        phase = s.phase
        amp = s.amplitude
        omega = s.omega
        inp = s.input_field

        neighbor_phase_sum = np.zeros_like(phase)
        neighbor_amp_sum = np.zeros_like(amp)
        for b in range(NUM_BANDS):
            neighbor_phase_sum[b] = toroidal_roll_sum(np.sin(phase[b]))
            neighbor_amp_sum[b] = toroidal_roll_sum(amp[b])

        # Cross-spectral influence at each spatial position.
        # For every band b, combine all band amplitudes/phases at the same lattice site.
        cross_phase_drive = np.zeros_like(phase)
        cross_amp_drive = np.zeros_like(amp)
        sin_phase = np.sin(phase)
        cos_phase = np.cos(phase)

        # Weighted sums over the band dimension.
        weighted_sin = np.tensordot(cross_w, sin_phase, axes=(1, 0))
        weighted_cos = np.tensordot(cross_w, cos_phase, axes=(1, 0))
        weighted_amp = np.tensordot(cross_w, amp, axes=(1, 0))

        # Phase pull toward weighted composite phase.
        target_phase = np.arctan2(weighted_sin, weighted_cos)
        cross_phase_drive = np.sin(target_phase - phase)
        cross_amp_drive = weighted_amp - amp

        # Phase dynamics.
        dphase = (
            omega
            + cfg.phase_coupling_local * neighbor_phase_sum
            + cfg.phase_coupling_cross * cross_phase_drive
            + cfg.input_phase_gain * inp
        )
        phase = (phase + dt * dphase) % (2.0 * math.pi)

        # Amplitude dynamics.
        damp = (
            cfg.amplitude_alpha * amp
            - cfg.amplitude_beta * (amp ** 3)
            + cfg.amplitude_neighbor_gain * (neighbor_amp_sum / 4.0 - amp)
            + cfg.amplitude_cross_gain * cross_amp_drive
            + cfg.amplitude_input_gain * inp
            - cfg.amplitude_decay * amp
        )
        amp = clip01(amp + dt * damp)

        s.phase = phase
        s.amplitude = amp
        self._update_memory()

        gc = self.global_coherence()
        s.coherence_history.append(gc)
        if len(s.coherence_history) > cfg.coherence_window:
            s.coherence_history.pop(0)
        s.step_count += 1

        return self.symbolic_readout()

    def run(
        self,
        steps: int,
        dream_mode: bool = False,
        log_every: int = 0,
    ) -> List[Dict[str, object]]:
        history: List[Dict[str, object]] = []
        for i in range(steps):
            summary = self.step(dream_mode=dream_mode)
            if log_every and (i % log_every == 0 or i == steps - 1):
                history.append({"step": self.state.step_count, **summary})
        return history

    # -------------------------------------------------------------------------
    # Convenience experiment helpers
    # -------------------------------------------------------------------------

    def pulse_sequence(
        self,
        pulses: List[Tuple[str, float, float, float, float, int]],
        clear_after_each: bool = True,
    ) -> List[Dict[str, object]]:
        """
        pulses: list of tuples
            (band_name, cx, cy, sigma, amplitude, steps)
        """
        results: List[Dict[str, object]] = []
        for band_name, cx, cy, sigma, amplitude, steps in pulses:
            self.inject_patch(band_name, cx, cy, sigma=sigma, amplitude=amplitude)
            results.extend(self.run(steps, dream_mode=False, log_every=max(1, steps // 4)))
            if clear_after_each:
                self.clear_input()
        return results

    def dream(self, steps: int = 100) -> List[Dict[str, object]]:
        self.clear_input()
        return self.run(steps=steps, dream_mode=True, log_every=max(1, steps // 5))

    def summary(self) -> str:
        readout = self.symbolic_readout()
        lines = [
            f"step={self.state.step_count}",
            f"global_coherence={readout['global_coherence']:.4f}",
            f"dominant_energy_band={readout['dominant_energy_band']} ({readout['dominant_energy']:.4f})",
            f"dominant_coherence_band={readout['dominant_coherence_band']} ({readout['dominant_coherence']:.4f})",
            "band_energies=" + ", ".join(
                f"{k}:{v:.3f}" for k, v in readout["band_energies"].items()
            ),
        ]
        return "\n".join(lines)


# -----------------------------------------------------------------------------
# Demo / example usage
# -----------------------------------------------------------------------------

def demo() -> None:
    print("Initializing Rainbow Resonance Processor v2...\n")
    rrp = RainbowResonanceProcessorV2(
        RRPv2Config(
            width=24,
            height=24,
            dt=0.025,
            seed=11,
            coherence_window=12,
        )
    )

    print("Initial state:")
    print(rrp.summary())
    print("\nInject symbolic concept: 'object'\n")
    rrp.inject_symbol("object")
    rrp.run(steps=60, log_every=0)
    print(rrp.summary())

    print("\nApply multi-band pulse sequence...\n")
    pulses = [
        ("red", 6, 12, 2.8, 1.0, 30),
        ("yellow", 12, 12, 3.2, 0.9, 30),
        ("blue", 18, 10, 2.5, 1.0, 30),
        ("violet", 12, 18, 3.0, 0.8, 30),
    ]
    rrp.pulse_sequence(pulses)
    print(rrp.summary())

    print("\nDream / drift retuning...\n")
    rrp.dream(steps=80)
    print(rrp.summary())

    print("\nMemory trace (7x7 cross-band plasticity):")
    np.set_printoptions(precision=3, suppress=True)
    print(rrp.state.memory_trace)

    print("\nFinal symbolic readout:")
    print(rrp.symbolic_readout())


if __name__ == "__main__":
    demo()
