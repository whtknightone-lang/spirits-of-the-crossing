"""
Rainbow Resonance Cortex RRC-1
=============================

GPU-ready research prototype for a layered synthetic cortex built from the
snowflake resonance processor.

What RRC-1 adds
---------------
- 6 cortical-style layers per spatial location
- a thalamic relay field for rhythmic gating
- a hippocampal attractor memory field
- feedforward, lateral, and feedback resonance flow
- autonomous developmental growth mode
- cortex-level readouts for integration, memory, and action tendency
- live visualization for cortical development

Layer mapping
-------------
The cortex uses six stacked resonance sheets inspired by cortical motifs:

    L1: integrative apical field
    L2: local association / feature binding
    L3: lateral coordination / communication
    L4: sensory relay input layer
    L5: projection / action preparation
    L6: feedback / predictive stabilization

Each cortical sheet still uses the same internal 7-point snowflake crystal:
1 center hub + 6 spectral arms.

This is not a biological brain model. It is a mathematically structured,
GPU-accelerated developmental cortex prototype for exploring resonance-based
brain formation.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, List, Tuple, Optional
import math
import torch
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation


# -----------------------------------------------------------------------------
# Spectral / crystal definitions
# -----------------------------------------------------------------------------

BAND_NAMES = ["red", "orange", "yellow", "green", "blue", "indigo", "violet"]
NUM_BANDS = len(BAND_NAMES)
BAND_INDEX = {name: i for i, name in enumerate(BAND_NAMES)}

POINT_NAMES = ["center", "arm_red", "arm_orange", "arm_yellow", "arm_blue", "arm_indigo", "arm_violet"]
NUM_POINTS = len(POINT_NAMES)
CENTER_POINT = 0

POINT_TO_BAND = torch.tensor([
    BAND_INDEX["green"],
    BAND_INDEX["red"],
    BAND_INDEX["orange"],
    BAND_INDEX["yellow"],
    BAND_INDEX["blue"],
    BAND_INDEX["indigo"],
    BAND_INDEX["violet"],
], dtype=torch.long)
BAND_TO_POINT = {int(b.item()): i for i, b in enumerate(POINT_TO_BAND)}

BASE_TONE_HZ = torch.tensor([261.63, 293.66, 329.63, 349.23, 392.00, 440.00, 493.88], dtype=torch.float32)
WAVELENGTH_NM = torch.tensor([680.0, 620.0, 580.0, 530.0, 470.0, 430.0, 400.0], dtype=torch.float32)

SQRT3 = math.sqrt(3.0)
CRYSTAL_COORDS = torch.tensor([
    [0.0, 0.0],
    [1.0, 0.0],
    [0.5, SQRT3 / 2.0],
    [-0.5, SQRT3 / 2.0],
    [-1.0, 0.0],
    [-0.5, -SQRT3 / 2.0],
    [0.5, -SQRT3 / 2.0],
], dtype=torch.float32)

NORMALIZED_TONE = BASE_TONE_HZ / BASE_TONE_HZ.max()
NORMALIZED_WAVELENGTH = WAVELENGTH_NM / WAVELENGTH_NM.max()
BAND_MATH_EQUIVALENT = 0.5 * NORMALIZED_TONE + 0.5 * NORMALIZED_WAVELENGTH

LAYER_NAMES = ["L1", "L2", "L3", "L4", "L5", "L6"]
NUM_LAYERS = len(LAYER_NAMES)
LAYER_INDEX = {name: i for i, name in enumerate(LAYER_NAMES)}


# -----------------------------------------------------------------------------
# Utility functions
# -----------------------------------------------------------------------------

def pick_device(device: Optional[str] = None) -> torch.device:
    if device is not None:
        return torch.device(device)
    return torch.device("cuda" if torch.cuda.is_available() else "cpu")


def clip01(x: torch.Tensor) -> torch.Tensor:
    return torch.clamp(x, 0.0, 1.0)


def normalize_rows(m: torch.Tensor, eps: float = 1e-8) -> torch.Tensor:
    row_sums = torch.sum(torch.abs(m), dim=1, keepdim=True) + eps
    return m / row_sums


def toroidal_roll_sum(x: torch.Tensor) -> torch.Tensor:
    return (
        torch.roll(x, shifts=1, dims=0)
        + torch.roll(x, shifts=-1, dims=0)
        + torch.roll(x, shifts=1, dims=1)
        + torch.roll(x, shifts=-1, dims=1)
    )


def crystal_neighbor_matrix(device: torch.device) -> torch.Tensor:
    a = torch.zeros((NUM_POINTS, NUM_POINTS), dtype=torch.float32, device=device)
    for arm in range(1, NUM_POINTS):
        a[CENTER_POINT, arm] = 1.0
        a[arm, CENTER_POINT] = 1.0
    ring_points = list(range(1, NUM_POINTS))
    for idx, p in enumerate(ring_points):
        q = ring_points[(idx + 1) % len(ring_points)]
        a[p, q] = 1.0
        a[q, p] = 1.0
    return a


# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------

@dataclass
class RRC1Config:
    width: int = 48
    height: int = 48
    dt: float = 0.02
    seed: int = 7
    device: Optional[str] = None

    phase_coupling_spatial: float = 0.14
    phase_coupling_crystal: float = 0.34
    phase_coupling_crossband: float = 0.16
    phase_coupling_layer: float = 0.10
    input_phase_gain: float = 0.35

    amplitude_alpha: float = 0.90
    amplitude_beta: float = 1.00
    amplitude_spatial_gain: float = 0.06
    amplitude_crystal_gain: float = 0.20
    amplitude_crossband_gain: float = 0.08
    amplitude_layer_gain: float = 0.10
    amplitude_input_gain: float = 0.80
    amplitude_decay: float = 0.06

    memory_lr: float = 0.0015
    memory_decay: float = 0.0002
    memory_strength: float = 0.25

    drift_frequency_noise: float = 0.003
    drift_cross_coupling_noise: float = 0.0015
    drift_amplitude_noise: float = 0.008

    coherence_window: int = 10
    max_input_amplitude: float = 1.0
    frequency_scale: float = 2.0 * math.pi / 500.0

    symmetry_gain: float = 0.12
    geometric_bias_gain: float = 0.08
    thalamic_gain: float = 0.08
    hippocampal_gain: float = 0.06


# -----------------------------------------------------------------------------
# Input encoding
# -----------------------------------------------------------------------------

class SnowflakeSpectralEncoder:
    def __init__(self, width: int, height: int, device: torch.device):
        self.width = width
        self.height = height
        self.device = device
        yy, xx = torch.meshgrid(
            torch.arange(height, device=device, dtype=torch.float32),
            torch.arange(width, device=device, dtype=torch.float32),
            indexing="ij",
        )
        self.xx = xx
        self.yy = yy

    def gaussian_patch(self, cx: float, cy: float, sigma: float, amplitude: float) -> torch.Tensor:
        d2 = (self.xx - cx) ** 2 + (self.yy - cy) ** 2
        return amplitude * torch.exp(-d2 / (2.0 * sigma * sigma))

    def encode_crystal_patch(
        self,
        point: int,
        cx: float,
        cy: float,
        sigma: float = 3.0,
        amplitude: float = 1.0,
        spread_to_neighbors: float = 0.25,
    ) -> torch.Tensor:
        fields = torch.zeros((NUM_POINTS, self.height, self.width), dtype=torch.float32, device=self.device)
        base = self.gaussian_patch(cx, cy, sigma, amplitude)
        fields[point] += base
        adj = crystal_neighbor_matrix(self.device)
        neighbors = torch.where(adj[point] > 0)[0]
        for n in neighbors.tolist():
            fields[n] += self.gaussian_patch(cx, cy, sigma * 1.10, amplitude * spread_to_neighbors)
        return clip01(fields)

    def encode_band_vector(self, v: torch.Tensor, amplitude: float = 0.35) -> torch.Tensor:
        v = v.to(device=self.device, dtype=torch.float32).reshape(NUM_BANDS)
        v = torch.clamp(v, min=0.0)
        if torch.max(v) > 0:
            v = v / (torch.max(v) + 1e-8)
        fields = torch.zeros((NUM_POINTS, self.height, self.width), dtype=torch.float32, device=self.device)
        for band in range(NUM_BANDS):
            point = BAND_TO_POINT[band]
            fields[point, :, :] = amplitude * v[band]
        return clip01(fields)

    def encode_symbol(self, symbol: str) -> torch.Tensor:
        symbol = symbol.strip().lower()
        presets: Dict[str, torch.Tensor] = {
            "ground": torch.tensor([1.0, 0.4, 0.2, 0.3, 0.1, 0.0, 0.0]),
            "flow": torch.tensor([0.1, 0.3, 0.4, 0.6, 1.0, 0.5, 0.1]),
            "balance": torch.tensor([0.2, 0.3, 0.5, 1.0, 0.5, 0.3, 0.2]),
            "insight": torch.tensor([0.0, 0.1, 0.2, 0.3, 0.5, 0.9, 1.0]),
            "object": torch.tensor([0.6, 0.5, 0.8, 0.6, 0.4, 0.2, 0.1]),
            "communication": torch.tensor([0.1, 0.2, 0.4, 0.5, 1.0, 0.7, 0.3]),
        }
        v = presets.get(symbol, torch.ones(NUM_BANDS) * 0.15)
        return self.encode_band_vector(v)


# -----------------------------------------------------------------------------
# Cortex core
# -----------------------------------------------------------------------------

@dataclass
class CortexState:
    phase: torch.Tensor
    amplitude: torch.Tensor
    omega: torch.Tensor
    input_field: torch.Tensor
    memory_trace: torch.Tensor
    thalamic_phase: torch.Tensor
    thalamic_amplitude: torch.Tensor
    hippocampal_trace: torch.Tensor
    coherence_history: List[float] = field(default_factory=list)
    step_count: int = 0


class RainbowResonanceCortexRRC1:
    def __init__(self, config: Optional[RRC1Config] = None):
        self.cfg = config or RRC1Config()
        self.device = pick_device(self.cfg.device)
        torch.manual_seed(self.cfg.seed)
        self.encoder = SnowflakeSpectralEncoder(self.cfg.width, self.cfg.height, self.device)
        self.crystal_adj = crystal_neighbor_matrix(self.device)
        self.crystal_degree = torch.sum(self.crystal_adj, dim=1, keepdim=True)

        h, w = self.cfg.height, self.cfg.width

        omega_band = self.cfg.frequency_scale * BASE_TONE_HZ.to(self.device)
        omega_point = omega_band[POINT_TO_BAND.to(self.device)]
        omega = omega_point[None, :, None, None].repeat(NUM_LAYERS, 1, h, w)
        layer_offsets = torch.linspace(-0.03, 0.03, NUM_LAYERS, device=self.device)[:, None, None, None]
        omega = omega + layer_offsets + 0.01 * torch.randn((NUM_LAYERS, NUM_POINTS, h, w), device=self.device)

        phase = 2.0 * math.pi * torch.rand((NUM_LAYERS, NUM_POINTS, h, w), device=self.device)
        amplitude = clip01(0.10 + 0.04 * torch.randn((NUM_LAYERS, NUM_POINTS, h, w), device=self.device))
        input_field = torch.zeros((NUM_LAYERS, NUM_POINTS, h, w), dtype=torch.float32, device=self.device)
        memory_trace = 0.2 * torch.eye(NUM_BANDS, dtype=torch.float32, device=self.device)[None, :, :].repeat(NUM_LAYERS, 1, 1)
        thalamic_phase = 2.0 * math.pi * torch.rand((NUM_POINTS, h, w), device=self.device)
        thalamic_amplitude = clip01(0.08 + 0.03 * torch.randn((NUM_POINTS, h, w), device=self.device))
        hippocampal_trace = torch.zeros((NUM_BANDS, h, w), dtype=torch.float32, device=self.device)

        self.cross_coupling = self._make_default_cross_coupling()
        self.geometric_bias = self._make_geometric_bias()
        self.layer_coupling = self._make_layer_coupling()

        self.state = CortexState(
            phase=phase,
            amplitude=amplitude,
            omega=omega,
            input_field=input_field,
            memory_trace=memory_trace,
            thalamic_phase=thalamic_phase,
            thalamic_amplitude=thalamic_amplitude,
            hippocampal_trace=hippocampal_trace,
        )

    def _make_default_cross_coupling(self) -> torch.Tensor:
        c = torch.zeros((NUM_BANDS, NUM_BANDS), dtype=torch.float32, device=self.device)
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
        c.fill_diagonal_(c.diagonal() + 0.35)
        return normalize_rows(c)

    def _make_geometric_bias(self) -> torch.Tensor:
        coords = CRYSTAL_COORDS.to(self.device)
        d = torch.cdist(coords, coords, p=2)
        geom = torch.exp(-d)
        point_band_math = BAND_MATH_EQUIVALENT.to(self.device)[POINT_TO_BAND.to(self.device)]
        math_outer = point_band_math[:, None] * point_band_math[None, :]
        geom = geom * (0.75 + 0.25 * math_outer)
        return normalize_rows(geom)

    def _make_layer_coupling(self) -> torch.Tensor:
        c = torch.zeros((NUM_LAYERS, NUM_LAYERS), dtype=torch.float32, device=self.device)
        for i in range(NUM_LAYERS):
            for j in range(NUM_LAYERS):
                c[i, j] = math.exp(-0.8 * abs(i - j))
        c[LAYER_INDEX["L4"], :] += 0.18
        c[:, LAYER_INDEX["L4"]] += 0.10
        c[LAYER_INDEX["L6"], :] += 0.16
        c[LAYER_INDEX["L5"], :] += 0.12
        c.fill_diagonal_(c.diagonal() + 0.40)
        return normalize_rows(c)

    def clear_input(self) -> None:
        self.state.input_field.zero_()

    def set_input_field(self, field: torch.Tensor, layer: Optional[int] = None) -> None:
        field = field.to(device=self.device, dtype=torch.float32)
        if layer is None:
            if tuple(field.shape) == tuple(self.state.input_field.shape):
                self.state.input_field = clip01(field) * self.cfg.max_input_amplitude
                return
            if tuple(field.shape) == tuple(self.state.input_field[0].shape):
                self.state.input_field.zero_()
                self.state.input_field[LAYER_INDEX["L4"]] = clip01(field) * self.cfg.max_input_amplitude
                return
            raise ValueError(f"Unexpected input field shape {tuple(field.shape)}")
        self.state.input_field[layer] = clip01(field) * self.cfg.max_input_amplitude

    def inject_patch(self, band_name: str, cx: float, cy: float, sigma: float = 3.0, amplitude: float = 1.0, spread_to_neighbors: float = 0.25) -> None:
        band = BAND_INDEX[band_name.lower()]
        point = BAND_TO_POINT[band]
        field = self.encoder.encode_crystal_patch(point, cx, cy, sigma, amplitude, spread_to_neighbors)
        self.set_input_field(field, layer=LAYER_INDEX["L4"])

    def inject_symbol(self, symbol: str) -> None:
        self.set_input_field(self.encoder.encode_symbol(symbol), layer=LAYER_INDEX["L4"])

    def point_energy(self) -> torch.Tensor:
        return torch.mean(self.state.amplitude, dim=(2, 3))

    def band_energy(self) -> torch.Tensor:
        e_point = self.point_energy()
        e_band = torch.zeros((NUM_LAYERS, NUM_BANDS), device=self.device)
        for point in range(NUM_POINTS):
            e_band[:, POINT_TO_BAND[point]] += e_point[:, point]
        return e_band

    def cortex_band_energy(self) -> torch.Tensor:
        return torch.mean(self.band_energy(), dim=0)

    def layer_energy(self) -> torch.Tensor:
        return torch.mean(self.state.amplitude, dim=(1, 2, 3))

    def point_phase_vector(self) -> torch.Tensor:
        z = self.state.amplitude * torch.exp(1j * self.state.phase)
        return torch.mean(z, dim=(2, 3))

    def band_phase_vector(self) -> torch.Tensor:
        z_point = self.point_phase_vector()
        z_band = torch.zeros((NUM_LAYERS, NUM_BANDS), dtype=torch.complex64, device=self.device)
        for point in range(NUM_POINTS):
            z_band[:, POINT_TO_BAND[point]] += z_point[:, point]
        return z_band

    def cortex_band_phase_vector(self) -> torch.Tensor:
        return torch.mean(self.band_phase_vector(), dim=0)

    def global_coherence(self) -> float:
        z = self.state.amplitude * torch.exp(1j * self.state.phase)
        return float(torch.abs(torch.mean(z)).detach().cpu())

    def layer_coherence(self) -> torch.Tensor:
        z = self.state.amplitude * torch.exp(1j * self.state.phase)
        return torch.abs(torch.mean(z, dim=(1, 2, 3)))

    def per_band_coherence(self) -> torch.Tensor:
        return torch.abs(self.cortex_band_phase_vector())

    def crystal_symmetry(self) -> float:
        ring = self.point_energy()[:, 1:]
        mean_ring = torch.mean(ring, dim=1, keepdim=True)
        symmetry = 1.0 / (1.0 + torch.mean(torch.abs(ring - mean_ring)))
        return float(symmetry.detach().cpu())

    def symbolic_readout(self) -> Dict[str, object]:
        energies = self.cortex_band_energy()
        coherence = self.per_band_coherence()
        layer_energy = self.layer_energy()
        layer_coh = self.layer_coherence()
        idx_energy = int(torch.argmax(energies).item())
        idx_coherence = int(torch.argmax(coherence).item())
        idx_layer = int(torch.argmax(layer_energy).item())
        hippocampal_strength = float(torch.mean(torch.abs(self.state.hippocampal_trace)).detach().cpu())
        return {
            "dominant_energy_band": BAND_NAMES[idx_energy],
            "dominant_energy": float(energies[idx_energy].detach().cpu()),
            "dominant_coherence_band": BAND_NAMES[idx_coherence],
            "dominant_coherence": float(coherence[idx_coherence].detach().cpu()),
            "dominant_layer": LAYER_NAMES[idx_layer],
            "dominant_layer_energy": float(layer_energy[idx_layer].detach().cpu()),
            "global_coherence": self.global_coherence(),
            "crystal_symmetry": self.crystal_symmetry(),
            "hippocampal_strength": hippocampal_strength,
            "band_energies": {BAND_NAMES[i]: float(energies[i].detach().cpu()) for i in range(NUM_BANDS)},
            "band_coherences": {BAND_NAMES[i]: float(coherence[i].detach().cpu()) for i in range(NUM_BANDS)},
            "layer_energies": {LAYER_NAMES[i]: float(layer_energy[i].detach().cpu()) for i in range(NUM_LAYERS)},
            "layer_coherences": {LAYER_NAMES[i]: float(layer_coh[i].detach().cpu()) for i in range(NUM_LAYERS)},
        }

    def _cross_band_weight_matrix(self) -> torch.Tensor:
        learned = self.cfg.memory_strength * self.state.memory_trace
        out = self.cross_coupling[None, :, :].repeat(NUM_LAYERS, 1, 1) + learned
        row_sums = torch.sum(torch.abs(out), dim=2, keepdim=True) + 1e-8
        return out / row_sums

    def _update_memory(self) -> None:
        band_z = self.band_phase_vector()
        amp = torch.abs(band_z)
        ang = torch.angle(band_z)
        phase_diff = ang[:, :, None] - ang[:, None, :]
        hebb = amp[:, :, None] * amp[:, None, :] * torch.cos(phase_diff)
        self.state.memory_trace *= (1.0 - self.cfg.memory_decay)
        self.state.memory_trace += self.cfg.memory_lr * hebb.real.to(torch.float32)
        self.state.memory_trace = torch.clamp(self.state.memory_trace, -1.0, 1.0)
        cortex_band = self.cortex_band_energy()[:, None, None]
        self.state.hippocampal_trace *= (1.0 - self.cfg.memory_decay)
        self.state.hippocampal_trace += self.cfg.memory_lr * cortex_band

    def _crystal_neighbor_mean(self, x: torch.Tensor) -> torch.Tensor:
        outs = []
        for layer in range(NUM_LAYERS):
            flat = x[layer].permute(1, 2, 0).reshape(-1, NUM_POINTS)
            neighbor_sum = flat @ self.crystal_adj.T
            neighbor_mean = neighbor_sum / torch.clamp(self.crystal_degree.T, min=1.0)
            outs.append(neighbor_mean.reshape(self.cfg.height, self.cfg.width, NUM_POINTS).permute(2, 0, 1))
        return torch.stack(outs, dim=0)

    def _symmetry_drive(self, amp: torch.Tensor) -> torch.Tensor:
        ring = amp[1:]
        ring_mean = torch.mean(ring, dim=0, keepdim=True)
        drive = torch.zeros_like(amp)
        drive[1:] = ring_mean - ring
        return drive

    @torch.no_grad()
    def step(self, dream_mode: bool = False) -> Dict[str, object]:
        s = self.state
        cfg = self.cfg
        dt = cfg.dt

        if dream_mode:
            s.omega += cfg.drift_frequency_noise * torch.randn_like(s.omega)
            self.cross_coupling += cfg.drift_cross_coupling_noise * torch.randn_like(self.cross_coupling)
            self.cross_coupling = normalize_rows(self.cross_coupling)
            s.amplitude = clip01(s.amplitude + cfg.drift_amplitude_noise * torch.randn_like(s.amplitude))

        cross_w = self._cross_band_weight_matrix()
        layer_w = self.layer_coupling

        phase = s.phase
        amp = s.amplitude
        omega = s.omega
        inp = s.input_field

        s.thalamic_phase = (s.thalamic_phase + dt * (cfg.frequency_scale * 0.5 + 0.25 * torch.sin(s.thalamic_phase))) % (2.0 * math.pi)
        s.thalamic_amplitude = clip01(s.thalamic_amplitude + dt * (0.12 * torch.sin(s.thalamic_phase) + 0.05 * torch.mean(amp[LAYER_INDEX['L4']], dim=0)))
        thalamic_drive = s.thalamic_amplitude * torch.sin(s.thalamic_phase)

        spatial_phase_sum = torch.stack([
            torch.stack([toroidal_roll_sum(torch.sin(phase[layer, p])) for p in range(NUM_POINTS)], dim=0)
            for layer in range(NUM_LAYERS)
        ], dim=0)
        spatial_amp_sum = torch.stack([
            torch.stack([toroidal_roll_sum(amp[layer, p]) for p in range(NUM_POINTS)], dim=0)
            for layer in range(NUM_LAYERS)
        ], dim=0)

        crystal_sin_mean = self._crystal_neighbor_mean(torch.sin(phase))
        crystal_cos_mean = self._crystal_neighbor_mean(torch.cos(phase))
        crystal_target_phase = torch.atan2(crystal_sin_mean, crystal_cos_mean)
        crystal_phase_drive = torch.sin(crystal_target_phase - phase)
        crystal_amp_drive = self._crystal_neighbor_mean(amp) - amp

        band_sin = torch.zeros((NUM_LAYERS, NUM_BANDS, self.cfg.height, self.cfg.width), dtype=torch.float32, device=self.device)
        band_cos = torch.zeros_like(band_sin)
        band_amp = torch.zeros_like(band_sin)
        sin_phase = torch.sin(phase)
        cos_phase = torch.cos(phase)
        for point in range(NUM_POINTS):
            band = int(POINT_TO_BAND[point].item())
            band_sin[:, band] += sin_phase[:, point]
            band_cos[:, band] += cos_phase[:, point]
            band_amp[:, band] += amp[:, point]

        weighted_band_sin = torch.stack([torch.tensordot(cross_w[layer], band_sin[layer], dims=([1], [0])) for layer in range(NUM_LAYERS)], dim=0)
        weighted_band_cos = torch.stack([torch.tensordot(cross_w[layer], band_cos[layer], dims=([1], [0])) for layer in range(NUM_LAYERS)], dim=0)
        weighted_band_amp = torch.stack([torch.tensordot(cross_w[layer], band_amp[layer], dims=([1], [0])) for layer in range(NUM_LAYERS)], dim=0)

        cross_phase_drive = torch.zeros_like(phase)
        cross_amp_drive = torch.zeros_like(amp)
        for point in range(NUM_POINTS):
            band = int(POINT_TO_BAND[point].item())
            target_phase = torch.atan2(weighted_band_sin[:, band], weighted_band_cos[:, band])
            cross_phase_drive[:, point] = torch.sin(target_phase - phase[:, point])
            cross_amp_drive[:, point] = weighted_band_amp[:, band] - amp[:, point]

        layer_mean_phase = torch.mean(torch.sin(phase), dim=1)
        layer_mean_cos = torch.mean(torch.cos(phase), dim=1)
        layer_target_sin = torch.stack([torch.tensordot(layer_w[layer], layer_mean_phase, dims=([0], [0])) for layer in range(NUM_LAYERS)], dim=0)
        layer_target_cos = torch.stack([torch.tensordot(layer_w[layer], layer_mean_cos, dims=([0], [0])) for layer in range(NUM_LAYERS)], dim=0)
        layer_phase_target = torch.atan2(layer_target_sin[:, None], layer_target_cos[:, None])
        layer_phase_drive = torch.sin(layer_phase_target - phase)

        layer_amp_mean = torch.mean(amp, dim=1)
        layer_amp_target = torch.stack([torch.tensordot(layer_w[layer], layer_amp_mean, dims=([0], [0])) for layer in range(NUM_LAYERS)], dim=0)
        layer_amp_drive = layer_amp_target[:, None] - amp

        point_math = BAND_MATH_EQUIVALENT.to(self.device)[POINT_TO_BAND.to(self.device)]
        geometric_drive = torch.zeros_like(amp)
        for i in range(NUM_POINTS):
            for j in range(NUM_POINTS):
                if i == j:
                    continue
                geometric_drive[:, i] += self.geometric_bias[i, j] * (point_math[j] * amp[:, j] - point_math[i] * amp[:, i])

        symmetry_drive = torch.stack([self._symmetry_drive(amp[layer]) for layer in range(NUM_LAYERS)], dim=0)

        hippocampal_bias = torch.zeros_like(amp)
        for point in range(NUM_POINTS):
            band = int(POINT_TO_BAND[point].item())
            replay = s.hippocampal_trace[band]
            hippocampal_bias[LAYER_INDEX['L2'], point] += replay
            hippocampal_bias[LAYER_INDEX['L3'], point] += 0.8 * replay
            hippocampal_bias[LAYER_INDEX['L1'], point] += 0.5 * replay

        dphase = (
            omega
            + cfg.phase_coupling_spatial * spatial_phase_sum
            + cfg.phase_coupling_crystal * crystal_phase_drive
            + cfg.phase_coupling_crossband * cross_phase_drive
            + cfg.phase_coupling_layer * layer_phase_drive
            + cfg.input_phase_gain * inp
            + cfg.thalamic_gain * thalamic_drive.unsqueeze(0)
        )
        phase = (phase + dt * dphase) % (2.0 * math.pi)

        damp = (
            cfg.amplitude_alpha * amp
            - cfg.amplitude_beta * (amp ** 3)
            + cfg.amplitude_spatial_gain * (spatial_amp_sum / 4.0 - amp)
            + cfg.amplitude_crystal_gain * crystal_amp_drive
            + cfg.amplitude_crossband_gain * cross_amp_drive
            + cfg.amplitude_layer_gain * layer_amp_drive
            + cfg.amplitude_input_gain * inp
            - cfg.amplitude_decay * amp
            + cfg.symmetry_gain * symmetry_drive
            + cfg.geometric_bias_gain * geometric_drive
            + cfg.thalamic_gain * thalamic_drive.unsqueeze(0)
            + cfg.hippocampal_gain * hippocampal_bias
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

    @torch.no_grad()
    def run(self, steps: int, dream_mode: bool = False, log_every: int = 0) -> List[Dict[str, object]]:
        history: List[Dict[str, object]] = []
        for i in range(steps):
            summary = self.step(dream_mode=dream_mode)
            if log_every and (i % log_every == 0 or i == steps - 1):
                history.append({"step": self.state.step_count, **summary})
        return history

    def developmental_input(self) -> torch.Tensor:
        t = self.state.step_count * self.cfg.dt
        base = torch.sin(self.state.omega * t)
        layer_bias = torch.linspace(0.85, 1.15, NUM_LAYERS, device=self.device)[:, None, None, None]
        thalamic = self.state.thalamic_amplitude.unsqueeze(0) * torch.sin(self.state.thalamic_phase.unsqueeze(0))
        noise = 0.05 * torch.randn_like(base)
        return clip01(0.08 * layer_bias * base + 0.06 * thalamic + noise)

    def developmental_step(self) -> Dict[str, object]:
        self.state.input_field = self.developmental_input()
        return self.step(dream_mode=False)

    def developmental_cycle(self, steps: int = 500) -> List[Dict[str, object]]:
        history: List[Dict[str, object]] = []
        for _ in range(steps):
            history.append(self.developmental_step())
        return history

    def summary(self) -> str:
        readout = self.symbolic_readout()
        lines = [
            f"device={self.device}",
            f"step={self.state.step_count}",
            f"global_coherence={readout['global_coherence']:.4f}",
            f"crystal_symmetry={readout['crystal_symmetry']:.4f}",
            f"hippocampal_strength={readout['hippocampal_strength']:.4f}",
            f"dominant_energy_band={readout['dominant_energy_band']} ({readout['dominant_energy']:.4f})",
            f"dominant_layer={readout['dominant_layer']} ({readout['dominant_layer_energy']:.4f})",
            "band_energies=" + ", ".join(f"{k}:{v:.3f}" for k, v in readout["band_energies"].items()),
            "layer_energies=" + ", ".join(f"{k}:{v:.3f}" for k, v in readout["layer_energies"].items()),
        ]
        return "\n".join(lines)

    def crystal_frame(self, cell_x: Optional[int] = None, cell_y: Optional[int] = None, phase_mode: bool = False, layer: int = 0) -> Tuple[torch.Tensor, torch.Tensor, List[str]]:
        if cell_x is None:
            cell_x = self.cfg.width // 2
        if cell_y is None:
            cell_y = self.cfg.height // 2
        amp = self.state.amplitude[layer, :, cell_y, cell_x].detach().cpu()
        phase = self.state.phase[layer, :, cell_y, cell_x].detach().cpu()
        labels = [BAND_NAMES[int(POINT_TO_BAND[i].item())] for i in range(NUM_POINTS)]
        if phase_mode:
            return CRYSTAL_COORDS.detach().cpu(), phase, labels
        return CRYSTAL_COORDS.detach().cpu(), amp, labels

    def band_image(self, layer: Optional[int] = None) -> torch.Tensor:
        amp = self.state.amplitude.detach()
        amp = torch.mean(amp, dim=0) if layer is None else amp[layer]
        band_amp = torch.zeros((NUM_BANDS, self.cfg.height, self.cfg.width), dtype=torch.float32, device=self.device)
        for point in range(NUM_POINTS):
            band = int(POINT_TO_BAND[point].item())
            band_amp[band] += amp[point]
        red = torch.clamp(band_amp[BAND_INDEX['red']] + 0.7 * band_amp[BAND_INDEX['orange']] + 0.35 * band_amp[BAND_INDEX['yellow']], 0.0, 1.0)
        green = torch.clamp(0.4 * band_amp[BAND_INDEX['yellow']] + band_amp[BAND_INDEX['green']] + 0.15 * band_amp[BAND_INDEX['orange']], 0.0, 1.0)
        blue = torch.clamp(band_amp[BAND_INDEX['blue']] + 0.75 * band_amp[BAND_INDEX['indigo']] + 0.55 * band_amp[BAND_INDEX['violet']], 0.0, 1.0)
        image = torch.stack([red, green, blue], dim=-1)
        image = image / (torch.max(image) + 1e-6)
        return image.detach().cpu()

    def live_visualize(self, steps_per_frame: int = 2, frames: int = 300, interval: int = 40, mode: str = 'amplitude', inject_symbol: Optional[str] = 'balance', layer: int = 0):
        if inject_symbol:
            self.inject_symbol(inject_symbol)

        fig = plt.figure(figsize=(14, 6))
        gs = fig.add_gridspec(1, 3, width_ratios=[1.0, 1.35, 1.15])
        ax_crystal = fig.add_subplot(gs[0, 0])
        ax_field = fig.add_subplot(gs[0, 1])
        ax_energy = fig.add_subplot(gs[0, 2])

        coords = CRYSTAL_COORDS.detach().cpu().numpy()
        crystal_scatter = ax_crystal.scatter(coords[:, 0], coords[:, 1], s=850)
        for i, (x, y) in enumerate(coords):
            ax_crystal.text(x, y + 0.18, BAND_NAMES[int(POINT_TO_BAND[i].item())], ha='center', va='bottom', fontsize=9)
        for i in range(NUM_POINTS):
            for j in range(i + 1, NUM_POINTS):
                if self.crystal_adj[i, j] > 0:
                    ax_crystal.plot([coords[i, 0], coords[j, 0]], [coords[i, 1], coords[j, 1]], alpha=0.35)
        ax_crystal.set_title(f'Local Snowflake Crystal ({LAYER_NAMES[layer]})')
        ax_crystal.set_xlim(-1.6, 1.6)
        ax_crystal.set_ylim(-1.45, 1.45)
        ax_crystal.set_aspect('equal')
        ax_crystal.axis('off')

        if mode == 'phase':
            field0 = self.state.phase[layer, CENTER_POINT].detach().cpu().numpy()
            field_im = ax_field.imshow(field0, animated=True)
            ax_field.set_title(f'{LAYER_NAMES[layer]} Green Hub Phase Field')
        else:
            field0 = self.band_image(layer=layer).numpy()
            field_im = ax_field.imshow(field0, animated=True)
            ax_field.set_title(f'{LAYER_NAMES[layer]} Rainbow Cortex Field')
        ax_field.set_xticks([])
        ax_field.set_yticks([])

        energy_history = [[] for _ in range(NUM_LAYERS)]
        x_history: List[int] = []
        lines = []
        for li in range(NUM_LAYERS):
            line, = ax_energy.plot([], [], label=LAYER_NAMES[li])
            lines.append(line)
        ax_energy.set_title('Layer Energies')
        ax_energy.set_xlim(0, max(10, frames))
        ax_energy.set_ylim(0, 1.2)
        ax_energy.legend(loc='upper left', fontsize=8)

        title = fig.suptitle('Rainbow Resonance Cortex RRC-1 Live View', fontsize=14)

        def update(frame_idx: int):
            for _ in range(steps_per_frame):
                self.developmental_step()

            _, values, _ = self.crystal_frame(phase_mode=(mode == 'phase'), layer=layer)
            crystal_scatter.set_array(values.numpy())
            crystal_scatter.set_clim(float(values.min()), float(max(values.max(), values.min() + 1e-6)))

            if mode == 'phase':
                field_im.set_data(self.state.phase[layer, CENTER_POINT].detach().cpu().numpy())
            else:
                field_im.set_data(self.band_image(layer=layer).numpy())

            energies = self.layer_energy().detach().cpu().numpy()
            x_history.append(self.state.step_count)
            for li in range(NUM_LAYERS):
                energy_history[li].append(float(energies[li]))
                lines[li].set_data(x_history, energy_history[li])

            ax_energy.set_xlim(0, max(20, self.state.step_count))
            ymax = max(1.0, max((max(hist) if hist else 0.0) for hist in energy_history) * 1.1)
            ax_energy.set_ylim(0, ymax)

            readout = self.symbolic_readout()
            title.set_text(
                f'RRC-1 Live View | step={self.state.step_count} | coherence={readout["global_coherence"]:.3f} | '
                f'symmetry={readout["crystal_symmetry"]:.3f} | hippocampus={readout["hippocampal_strength"]:.3f} | '
                f'dominant_layer={readout["dominant_layer"]}'
            )
            return [crystal_scatter, field_im, *lines, title]

        animation = FuncAnimation(fig, update, frames=frames, interval=interval, blit=False, repeat=False)
        plt.tight_layout()
        plt.show()
        return animation


def demo() -> None:
    print("Initializing Rainbow Resonance Cortex RRC-1...\n")
    rrc = RainbowResonanceCortexRRC1(
        RRC1Config(
            width=40,
            height=40,
            dt=0.02,
            seed=11,
            coherence_window=12,
        )
    )

    print("Initial state:")
    print(rrc.summary())

    print("\nInject symbolic concept into L4 sensory relay: 'balance'\n")
    rrc.inject_symbol("balance")
    rrc.run(steps=80, log_every=0)
    print(rrc.summary())

    print("\nAutonomous developmental growth...\n")
    rrc.developmental_cycle(steps=120)
    print(rrc.summary())

    print("\nLayer memory trace (L x 7 x 7):")
    print(rrc.state.memory_trace.detach().cpu())

    print("\nHippocampal trace strength:")
    print(torch.mean(torch.abs(rrc.state.hippocampal_trace)).item())

    print("\nFinal symbolic readout:")
    print(rrc.symbolic_readout())

    # Uncomment to open the live cortex visualization window.
    # rrc.live_visualize(steps_per_frame=2, frames=240, interval=40, mode='amplitude', inject_symbol='balance', layer=0)


if __name__ == "__main__":
    demo()
