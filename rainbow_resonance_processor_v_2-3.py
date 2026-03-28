"""
Rainbow Resonance Cortex RRC-2
=============================

A more concise GPU-structured rethink of the cortex prototype.

Design goals
------------
- keep the 6 cortical layers
- keep the 7-point snowflake crystal per cell
- keep the 7 spectral bands / tones / wavelengths
- use a compact tensor-first GPU implementation
- avoid overgrowing the architecture with too many fragile subsystems
- preserve developmental dynamics, memory, prediction, and motor tendency
  in a minimal form

Core idea
---------
Instead of many separate brain-inspired modules, RRC-2 uses four compact GPU
state blocks:

1. cortex field      : [L, P, H, W] phase + amplitude
2. memory field      : [B, H, W] working + episodic traces
3. latent state      : [B] world + self + motor + prediction error
4. coupling tensors  : band / layer / crystal couplings

This keeps the model brain-like, but concise enough to remain understandable
and fast on GPU.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple
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

LAYER_NAMES = ["L1", "L2", "L3", "L4", "L5", "L6"]
NUM_LAYERS = len(LAYER_NAMES)
LAYER_INDEX = {name: i for i, name in enumerate(LAYER_NAMES)}

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

NORMALIZED_TONE = BASE_TONE_HZ / BASE_TONE_HZ.max()
NORMALIZED_WAVELENGTH = WAVELENGTH_NM / WAVELENGTH_NM.max()
BAND_MATH_EQUIVALENT = 0.5 * NORMALIZED_TONE + 0.5 * NORMALIZED_WAVELENGTH

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


# -----------------------------------------------------------------------------
# Utilities
# -----------------------------------------------------------------------------

def pick_device(device: Optional[str] = None) -> torch.device:
    if device is not None:
        return torch.device(device)
    return torch.device("cuda" if torch.cuda.is_available() else "cpu")


def clip01(x: torch.Tensor) -> torch.Tensor:
    return torch.clamp(x, 0.0, 1.0)


def normalize_rows(m: torch.Tensor, eps: float = 1e-8) -> torch.Tensor:
    return m / (torch.sum(torch.abs(m), dim=1, keepdim=True) + eps)


def toroidal_roll_sum(x: torch.Tensor) -> torch.Tensor:
    return (
        torch.roll(x, shifts=1, dims=-2)
        + torch.roll(x, shifts=-1, dims=-2)
        + torch.roll(x, shifts=1, dims=-1)
        + torch.roll(x, shifts=-1, dims=-1)
    )


def crystal_neighbor_matrix(device: torch.device) -> torch.Tensor:
    a = torch.zeros((NUM_POINTS, NUM_POINTS), dtype=torch.float32, device=device)
    for arm in range(1, NUM_POINTS):
        a[CENTER_POINT, arm] = 1.0
        a[arm, CENTER_POINT] = 1.0
    for i in range(1, NUM_POINTS):
        j = 1 + (i % 6)
        a[i, j] = 1.0
        a[j, i] = 1.0
    return a


# -----------------------------------------------------------------------------
# Configuration and state
# -----------------------------------------------------------------------------

@dataclass
class RRC2Config:
    width: int = 48
    height: int = 48
    dt: float = 0.02
    seed: int = 7
    device: Optional[str] = None

    phase_local_gain: float = 0.12
    phase_crystal_gain: float = 0.24
    phase_band_gain: float = 0.12
    phase_layer_gain: float = 0.08
    phase_predictive_gain: float = 0.08

    amp_alpha: float = 0.90
    amp_beta: float = 1.00
    amp_local_gain: float = 0.05
    amp_crystal_gain: float = 0.15
    amp_band_gain: float = 0.06
    amp_layer_gain: float = 0.08
    amp_input_gain: float = 0.75
    amp_decay: float = 0.06
    amp_memory_gain: float = 0.05
    amp_predictive_gain: float = 0.06

    memory_lr: float = 0.002
    memory_decay: float = 0.002
    semantic_lr: float = 0.002
    procedural_lr: float = 0.002
    latent_lr: float = 0.03

    drift_frequency_noise: float = 0.003
    drift_amplitude_noise: float = 0.008

    frequency_scale: float = 2.0 * math.pi / 500.0
    max_input_amplitude: float = 1.0
    coherence_window: int = 20


@dataclass
class CortexState:
    phase: torch.Tensor                # [L, P, H, W]
    amplitude: torch.Tensor            # [L, P, H, W]
    omega: torch.Tensor                # [L, P, H, W]
    input_field: torch.Tensor          # [L, P, H, W]

    working_memory: torch.Tensor       # [B, H, W]
    episodic_memory: torch.Tensor      # [B, H, W]
    semantic_memory: torch.Tensor      # [B]
    procedural_memory: torch.Tensor    # [B]

    world_state: torch.Tensor          # [B]
    self_state: torch.Tensor           # [B]
    motor_state: torch.Tensor          # [B]
    prediction_error: torch.Tensor     # [B]
    drive_state: torch.Tensor          # [4]

    band_memory: torch.Tensor          # [L, B, B]
    coherence_history: List[float] = field(default_factory=list)
    step_count: int = 0


# -----------------------------------------------------------------------------
# Input encoder
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

    def encode_band_vector(self, v: torch.Tensor, amplitude: float = 0.5) -> torch.Tensor:
        v = torch.clamp(v.to(self.device, dtype=torch.float32).reshape(NUM_BANDS), min=0.0)
        if torch.max(v) > 0:
            v = v / (torch.max(v) + 1e-8)
        out = torch.zeros((NUM_POINTS, self.height, self.width), dtype=torch.float32, device=self.device)
        for band in range(NUM_BANDS):
            out[BAND_TO_POINT[band]] = amplitude * v[band]
        return clip01(out)

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
        return self.encode_band_vector(presets.get(symbol, torch.ones(NUM_BANDS) * 0.15))


# -----------------------------------------------------------------------------
# RRC-2
# -----------------------------------------------------------------------------

class RainbowResonanceCortexRRC2:
    def __init__(self, config: Optional[RRC2Config] = None):
        self.cfg = config or RRC2Config()
        self.device = pick_device(self.cfg.device)
        torch.manual_seed(self.cfg.seed)

        self.encoder = SnowflakeSpectralEncoder(self.cfg.width, self.cfg.height, self.device)
        self.crystal_adj = crystal_neighbor_matrix(self.device)
        self.crystal_degree = torch.sum(self.crystal_adj, dim=1, keepdim=True)
        self.band_kernel = self._make_band_coupling()
        self.layer_kernel = self._make_layer_coupling()

        h, w = self.cfg.height, self.cfg.width
        omega_band = self.cfg.frequency_scale * BASE_TONE_HZ.to(self.device)
        omega_point = omega_band[POINT_TO_BAND.to(self.device)]
        omega = omega_point[None, :, None, None].repeat(NUM_LAYERS, 1, h, w)
        omega += torch.linspace(-0.03, 0.03, NUM_LAYERS, device=self.device)[:, None, None, None]
        omega += 0.01 * torch.randn((NUM_LAYERS, NUM_POINTS, h, w), device=self.device)

        self.state = CortexState(
            phase=2.0 * math.pi * torch.rand((NUM_LAYERS, NUM_POINTS, h, w), device=self.device),
            amplitude=clip01(0.10 + 0.04 * torch.randn((NUM_LAYERS, NUM_POINTS, h, w), device=self.device)),
            omega=omega,
            input_field=torch.zeros((NUM_LAYERS, NUM_POINTS, h, w), dtype=torch.float32, device=self.device),
            working_memory=torch.zeros((NUM_BANDS, h, w), dtype=torch.float32, device=self.device),
            episodic_memory=torch.zeros((NUM_BANDS, h, w), dtype=torch.float32, device=self.device),
            semantic_memory=torch.zeros((NUM_BANDS,), dtype=torch.float32, device=self.device),
            procedural_memory=torch.zeros((NUM_BANDS,), dtype=torch.float32, device=self.device),
            world_state=torch.zeros((NUM_BANDS,), dtype=torch.float32, device=self.device),
            self_state=torch.zeros((NUM_BANDS,), dtype=torch.float32, device=self.device),
            motor_state=torch.zeros((NUM_BANDS,), dtype=torch.float32, device=self.device),
            prediction_error=torch.zeros((NUM_BANDS,), dtype=torch.float32, device=self.device),
            drive_state=torch.tensor([0.5, 0.5, 0.5, 0.5], dtype=torch.float32, device=self.device),
            band_memory=0.2 * torch.eye(NUM_BANDS, dtype=torch.float32, device=self.device)[None].repeat(NUM_LAYERS, 1, 1),
        )

    # ------------------------------------------------------------------
    # Couplings
    # ------------------------------------------------------------------

    def _make_band_coupling(self) -> torch.Tensor:
        c = torch.zeros((NUM_BANDS, NUM_BANDS), dtype=torch.float32, device=self.device)
        for i in range(NUM_BANDS):
            for j in range(NUM_BANDS):
                c[i, j] = math.exp(-0.9 * abs(i - j))
        c[BAND_INDEX["green"], :] += 0.2
        c[:, BAND_INDEX["green"]] += 0.2
        c[BAND_INDEX["violet"], :] += 0.08
        c[:, BAND_INDEX["violet"]] += 0.05
        c.fill_diagonal_(c.diagonal() + 0.35)
        return normalize_rows(c)

    def _make_layer_coupling(self) -> torch.Tensor:
        c = torch.zeros((NUM_LAYERS, NUM_LAYERS), dtype=torch.float32, device=self.device)
        for i in range(NUM_LAYERS):
            for j in range(NUM_LAYERS):
                c[i, j] = math.exp(-0.8 * abs(i - j))
        c[LAYER_INDEX["L4"], :] += 0.14
        c[LAYER_INDEX["L6"], :] += 0.10
        c[LAYER_INDEX["L5"], :] += 0.08
        c.fill_diagonal_(c.diagonal() + 0.4)
        return normalize_rows(c)

    # ------------------------------------------------------------------
    # Input
    # ------------------------------------------------------------------

    def clear_input(self) -> None:
        self.state.input_field.zero_()

    def set_input_field(self, field: torch.Tensor, layer: int = LAYER_INDEX["L4"]) -> None:
        self.state.input_field.zero_()
        self.state.input_field[layer] = clip01(field.to(self.device, dtype=torch.float32)) * self.cfg.max_input_amplitude

    def inject_symbol(self, symbol: str) -> None:
        self.set_input_field(self.encoder.encode_symbol(symbol), layer=LAYER_INDEX["L4"])

    def set_multisensory_input(
        self,
        vision: Optional[torch.Tensor] = None,
        audio: Optional[torch.Tensor] = None,
        proprioception: Optional[torch.Tensor] = None,
    ) -> None:
        streams = []
        for x in (vision, audio, proprioception):
            if x is not None:
                streams.append(x.to(self.device, dtype=torch.float32).reshape(NUM_BANDS))
        if not streams:
            streams = [torch.zeros(NUM_BANDS, device=self.device)]
        fused = torch.mean(torch.stack(streams, dim=0), dim=0)
        self.set_input_field(self.encoder.encode_band_vector(fused, amplitude=0.5), layer=LAYER_INDEX["L4"])

    # ------------------------------------------------------------------
    # Measurements
    # ------------------------------------------------------------------

    def point_energy(self) -> torch.Tensor:
        return torch.mean(self.state.amplitude, dim=(2, 3))                       # [L,P]

    def band_energy(self) -> torch.Tensor:
        e = torch.zeros((NUM_LAYERS, NUM_BANDS), dtype=torch.float32, device=self.device)
        pe = self.point_energy()
        for p in range(NUM_POINTS):
            e[:, POINT_TO_BAND[p]] += pe[:, p]
        return e                                                                  # [L,B]

    def cortex_band_energy(self) -> torch.Tensor:
        return torch.mean(self.band_energy(), dim=0)                              # [B]

    def layer_energy(self) -> torch.Tensor:
        return torch.mean(self.state.amplitude, dim=(1, 2, 3))                    # [L]

    def global_coherence(self) -> float:
        z = self.state.amplitude * torch.exp(1j * self.state.phase)
        return float(torch.abs(torch.mean(z)).detach().cpu())

    def crystal_symmetry(self) -> float:
        ring = self.point_energy()[:, 1:]
        mean_ring = torch.mean(ring, dim=1, keepdim=True)
        symmetry = 1.0 / (1.0 + torch.mean(torch.abs(ring - mean_ring)))
        return float(symmetry.detach().cpu())

    def symbolic_readout(self) -> Dict[str, object]:
        be = self.cortex_band_energy()
        le = self.layer_energy()
        dominant_band = int(torch.argmax(be).item())
        dominant_layer = int(torch.argmax(le).item())
        return {
            "global_coherence": self.global_coherence(),
            "crystal_symmetry": self.crystal_symmetry(),
            "dominant_energy_band": BAND_NAMES[dominant_band],
            "dominant_energy": float(be[dominant_band].detach().cpu()),
            "dominant_layer": LAYER_NAMES[dominant_layer],
            "dominant_layer_energy": float(le[dominant_layer].detach().cpu()),
            "world_state_norm": float(torch.norm(self.state.world_state).detach().cpu()),
            "self_state_norm": float(torch.norm(self.state.self_state).detach().cpu()),
            "motor_state_norm": float(torch.norm(self.state.motor_state).detach().cpu()),
            "prediction_error_norm": float(torch.norm(self.state.prediction_error).detach().cpu()),
            "drives": {
                "curiosity": float(self.state.drive_state[0].detach().cpu()),
                "coherence": float(self.state.drive_state[1].detach().cpu()),
                "stability": float(self.state.drive_state[2].detach().cpu()),
                "exploration": float(self.state.drive_state[3].detach().cpu()),
            },
        }

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    def _crystal_neighbor_mean(self, x: torch.Tensor) -> torch.Tensor:
        # x: [L,P,H,W] -> [L,P,H,W]
        flat = x.permute(0, 2, 3, 1).reshape(-1, NUM_POINTS)
        neigh = flat @ self.crystal_adj.T
        neigh = neigh / torch.clamp(self.crystal_degree.T, min=1.0)
        return neigh.reshape(NUM_LAYERS, self.cfg.height, self.cfg.width, NUM_POINTS).permute(0, 3, 1, 2)

    def _band_tensor(self, x: torch.Tensor) -> torch.Tensor:
        # x: [L,P,H,W] -> [L,B,H,W]
        out = torch.zeros((NUM_LAYERS, NUM_BANDS, self.cfg.height, self.cfg.width), dtype=x.dtype, device=self.device)
        for p in range(NUM_POINTS):
            out[:, POINT_TO_BAND[p]] += x[:, p]
        return out

    def _band_backproject(self, bx: torch.Tensor) -> torch.Tensor:
        # bx: [L,B,H,W] -> [L,P,H,W]
        out = torch.zeros((NUM_LAYERS, NUM_POINTS, self.cfg.height, self.cfg.width), dtype=bx.dtype, device=self.device)
        for p in range(NUM_POINTS):
            out[:, p] = bx[:, POINT_TO_BAND[p]]
        return out

    def _update_memories(self) -> None:
        s = self.state
        cortex_band = self.cortex_band_energy()[:, None, None]                    # [B,1,1]
        s.working_memory = (1.0 - self.cfg.memory_decay) * s.working_memory + self.cfg.memory_lr * cortex_band
        s.episodic_memory = (1.0 - self.cfg.memory_decay) * s.episodic_memory + 0.5 * self.cfg.memory_lr * s.working_memory
        s.semantic_memory = (1.0 - self.cfg.memory_decay) * s.semantic_memory + self.cfg.semantic_lr * torch.mean(s.episodic_memory, dim=(1, 2))
        s.procedural_memory = (1.0 - self.cfg.memory_decay) * s.procedural_memory + self.cfg.procedural_lr * torch.abs(s.motor_state)

        band_vec = self.band_energy()                                             # [L,B]
        band_z = band_vec[:, :, None] * band_vec[:, None, :]
        s.band_memory = (1.0 - self.cfg.memory_decay) * s.band_memory + self.cfg.memory_lr * torch.tanh(band_z)
        s.band_memory = torch.clamp(s.band_memory, -1.0, 1.0)

    def _developmental_input(self) -> torch.Tensor:
        t = self.state.step_count * self.cfg.dt
        base = torch.sin(self.state.omega * t)
        noise = 0.05 * torch.randn_like(base)
        drive_scale = 0.05 + 0.05 * self.state.drive_state[0]
        return clip01(0.08 * base + drive_scale * noise)

    # ------------------------------------------------------------------
    # Dynamics
    # ------------------------------------------------------------------

    @torch.no_grad()
    def step(self, dream_mode: bool = False, autonomous: bool = False) -> Dict[str, object]:
        s = self.state
        cfg = self.cfg
        dt = cfg.dt

        if autonomous:
            s.input_field = self._developmental_input()

        if dream_mode:
            s.omega += cfg.drift_frequency_noise * torch.randn_like(s.omega)
            s.amplitude = clip01(s.amplitude + cfg.drift_amplitude_noise * torch.randn_like(s.amplitude))

        phase = s.phase
        amp = s.amplitude
        inp = s.input_field

        # Compact latent-state updates.
        cortex_band = self.cortex_band_energy()                                   # [B]
        s.prediction_error = cortex_band - s.world_state
        s.world_state = (1.0 - cfg.latent_lr) * s.world_state + cfg.latent_lr * (cortex_band + 0.2 * s.motor_state)
        s.self_state = (1.0 - cfg.latent_lr) * s.self_state + cfg.latent_lr * (0.6 * cortex_band + 0.4 * s.semantic_memory)
        s.motor_state = torch.tanh(self.band_energy()[LAYER_INDEX["L5"]] + 0.5 * s.procedural_memory - 0.3 * s.prediction_error)

        curiosity = torch.mean(torch.abs(s.prediction_error))
        coherence = torch.tensor(self.global_coherence(), device=self.device)
        stability = 1.0 / (1.0 + torch.std(self.layer_energy()))
        exploration = torch.mean(torch.abs(torch.sin(phase)))
        s.drive_state = torch.stack([curiosity, coherence, stability, exploration])

        # Spatial + crystal coupling.
        spatial_phase = toroidal_roll_sum(torch.sin(phase))
        spatial_amp = toroidal_roll_sum(amp)
        crystal_phase_target = torch.atan2(self._crystal_neighbor_mean(torch.sin(phase)), self._crystal_neighbor_mean(torch.cos(phase)))
        crystal_phase_drive = torch.sin(crystal_phase_target - phase)
        crystal_amp_drive = self._crystal_neighbor_mean(amp) - amp

        # Band coupling in compact tensor form.
        band_sin = self._band_tensor(torch.sin(phase))                            # [L,B,H,W]
        band_cos = self._band_tensor(torch.cos(phase))
        band_amp = self._band_tensor(amp)

        layer_band_kernel = normalize_rows(self.band_kernel[None] + cfg.memory_lr * s.band_memory)
        weighted_sin = torch.stack([torch.tensordot(layer_band_kernel[l], band_sin[l], dims=([1], [0])) for l in range(NUM_LAYERS)], dim=0)
        weighted_cos = torch.stack([torch.tensordot(layer_band_kernel[l], band_cos[l], dims=([1], [0])) for l in range(NUM_LAYERS)], dim=0)
        weighted_amp = torch.stack([torch.tensordot(layer_band_kernel[l], band_amp[l], dims=([1], [0])) for l in range(NUM_LAYERS)], dim=0)

        band_phase_drive = self._band_backproject(torch.sin(torch.atan2(weighted_sin, weighted_cos) - torch.atan2(band_sin, band_cos)))
        band_amp_drive = self._band_backproject(weighted_amp - band_amp)

        # Layer coupling in compact tensor form.
        layer_sin = torch.mean(torch.sin(phase), dim=1)                           # [L,H,W]
        layer_cos = torch.mean(torch.cos(phase), dim=1)
        layer_amp = torch.mean(amp, dim=1)
        target_layer_sin = torch.stack([torch.tensordot(self.layer_kernel[l], layer_sin, dims=([0], [0])) for l in range(NUM_LAYERS)], dim=0)
        target_layer_cos = torch.stack([torch.tensordot(self.layer_kernel[l], layer_cos, dims=([0], [0])) for l in range(NUM_LAYERS)], dim=0)
        target_layer_amp = torch.stack([torch.tensordot(self.layer_kernel[l], layer_amp, dims=([0], [0])) for l in range(NUM_LAYERS)], dim=0)
        layer_phase_drive = torch.sin(torch.atan2(target_layer_sin[:, None], target_layer_cos[:, None]) - phase)
        layer_amp_drive = target_layer_amp[:, None] - amp

        # Memory / prediction projection back into the field.
        memory_field = torch.zeros_like(amp)
        wm = torch.mean(s.working_memory, dim=(1, 2))
        em = torch.mean(s.episodic_memory, dim=(1, 2))
        for p in range(NUM_POINTS):
            b = int(POINT_TO_BAND[p].item())
            memory_field[LAYER_INDEX["L2"], p] += wm[b] + em[b]
            memory_field[LAYER_INDEX["L3"], p] += s.semantic_memory[b]
            memory_field[LAYER_INDEX["L1"], p] += s.self_state[b]
            memory_field[LAYER_INDEX["L6"], p] += -s.prediction_error[b] + s.world_state[b]
            memory_field[LAYER_INDEX["L5"], p] += s.motor_state[b]

        # Final updates.
        dphase = (
            s.omega
            + cfg.phase_local_gain * spatial_phase
            + cfg.phase_crystal_gain * crystal_phase_drive
            + cfg.phase_band_gain * band_phase_drive
            + cfg.phase_layer_gain * layer_phase_drive
            + cfg.phase_predictive_gain * memory_field
            + 0.35 * inp
        )
        damp = (
            cfg.amp_alpha * amp
            - cfg.amp_beta * (amp ** 3)
            + cfg.amp_local_gain * (spatial_amp / 4.0 - amp)
            + cfg.amp_crystal_gain * crystal_amp_drive
            + cfg.amp_band_gain * band_amp_drive
            + cfg.amp_layer_gain * layer_amp_drive
            + cfg.amp_memory_gain * memory_field
            + cfg.amp_predictive_gain * torch.tanh(memory_field)
            + cfg.amp_input_gain * inp
            - cfg.amp_decay * amp
        )

        s.phase = (phase + dt * dphase) % (2.0 * math.pi)
        s.amplitude = clip01(amp + dt * damp)
        self._update_memories()

        gc = self.global_coherence()
        s.coherence_history.append(gc)
        if len(s.coherence_history) > cfg.coherence_window:
            s.coherence_history.pop(0)
        s.step_count += 1
        return self.symbolic_readout()

    @torch.no_grad()
    def run(self, steps: int, dream_mode: bool = False, autonomous: bool = False, log_every: int = 0) -> List[Dict[str, object]]:
        history: List[Dict[str, object]] = []
        for i in range(steps):
            summary = self.step(dream_mode=dream_mode, autonomous=autonomous)
            if log_every and (i % log_every == 0 or i == steps - 1):
                history.append({"step": self.state.step_count, **summary})
        return history

    def developmental_cycle(self, steps: int = 500) -> List[Dict[str, object]]:
        return self.run(steps=steps, autonomous=True, log_every=0)

    # ------------------------------------------------------------------
    # Visualization
    # ------------------------------------------------------------------

    def crystal_frame(self, layer: int = 0, cell_x: Optional[int] = None, cell_y: Optional[int] = None, phase_mode: bool = False):
        if cell_x is None:
            cell_x = self.cfg.width // 2
        if cell_y is None:
            cell_y = self.cfg.height // 2
        values = self.state.phase[layer, :, cell_y, cell_x] if phase_mode else self.state.amplitude[layer, :, cell_y, cell_x]
        labels = [BAND_NAMES[int(POINT_TO_BAND[i].item())] for i in range(NUM_POINTS)]
        return CRYSTAL_COORDS.detach().cpu(), values.detach().cpu(), labels

    def band_image(self, layer: Optional[int] = None) -> torch.Tensor:
        amp = torch.mean(self.state.amplitude, dim=0) if layer is None else self.state.amplitude[layer]
        band_amp = torch.zeros((NUM_BANDS, self.cfg.height, self.cfg.width), dtype=torch.float32, device=self.device)
        for p in range(NUM_POINTS):
            band_amp[POINT_TO_BAND[p]] += amp[p]
        r = torch.clamp(band_amp[BAND_INDEX["red"]] + 0.7 * band_amp[BAND_INDEX["orange"]] + 0.35 * band_amp[BAND_INDEX["yellow"]], 0.0, 1.0)
        g = torch.clamp(0.4 * band_amp[BAND_INDEX["yellow"]] + band_amp[BAND_INDEX["green"]], 0.0, 1.0)
        b = torch.clamp(band_amp[BAND_INDEX["blue"]] + 0.75 * band_amp[BAND_INDEX["indigo"]] + 0.55 * band_amp[BAND_INDEX["violet"]], 0.0, 1.0)
        img = torch.stack([r, g, b], dim=-1)
        return (img / (torch.max(img) + 1e-6)).detach().cpu()

    def live_visualize(self, steps_per_frame: int = 2, frames: int = 300, interval: int = 40, layer: int = 0, mode: str = "amplitude", inject_symbol: Optional[str] = "balance"):
        if inject_symbol:
            self.inject_symbol(inject_symbol)

        fig = plt.figure(figsize=(14, 6))
        gs = fig.add_gridspec(1, 3, width_ratios=[1.0, 1.35, 1.15])
        ax_crystal = fig.add_subplot(gs[0, 0])
        ax_field = fig.add_subplot(gs[0, 1])
        ax_energy = fig.add_subplot(gs[0, 2])

        coords = CRYSTAL_COORDS.detach().cpu().numpy()
        sc = ax_crystal.scatter(coords[:, 0], coords[:, 1], s=850)
        for i, (x, y) in enumerate(coords):
            ax_crystal.text(x, y + 0.18, BAND_NAMES[int(POINT_TO_BAND[i].item())], ha="center", va="bottom", fontsize=9)
        for i in range(NUM_POINTS):
            for j in range(i + 1, NUM_POINTS):
                if self.crystal_adj[i, j] > 0:
                    ax_crystal.plot([coords[i, 0], coords[j, 0]], [coords[i, 1], coords[j, 1]], alpha=0.35)
        ax_crystal.set_title(f"Local Snowflake Crystal ({LAYER_NAMES[layer]})")
        ax_crystal.set_xlim(-1.6, 1.6)
        ax_crystal.set_ylim(-1.45, 1.45)
        ax_crystal.set_aspect("equal")
        ax_crystal.axis("off")

        if mode == "phase":
            im = ax_field.imshow(self.state.phase[layer, CENTER_POINT].detach().cpu().numpy(), animated=True)
            ax_field.set_title(f"{LAYER_NAMES[layer]} Green Hub Phase Field")
        else:
            im = ax_field.imshow(self.band_image(layer=layer).numpy(), animated=True)
            ax_field.set_title(f"{LAYER_NAMES[layer]} Rainbow Cortex Field")
        ax_field.set_xticks([])
        ax_field.set_yticks([])

        x_history: List[int] = []
        y_history = [[] for _ in range(NUM_LAYERS)]
        lines = [ax_energy.plot([], [], label=LAYER_NAMES[i])[0] for i in range(NUM_LAYERS)]
        ax_energy.set_title("Layer Energies")
        ax_energy.set_xlim(0, max(10, frames))
        ax_energy.set_ylim(0, 1.2)
        ax_energy.legend(loc="upper left", fontsize=8)

        title = fig.suptitle("Rainbow Resonance Cortex RRC-2 Live View", fontsize=14)

        def update(_frame: int):
            for _ in range(steps_per_frame):
                self.step(autonomous=True)

            _, vals, _ = self.crystal_frame(layer=layer, phase_mode=(mode == "phase"))
            sc.set_array(vals.numpy())
            sc.set_clim(float(vals.min()), float(max(vals.max(), vals.min() + 1e-6)))

            if mode == "phase":
                im.set_data(self.state.phase[layer, CENTER_POINT].detach().cpu().numpy())
            else:
                im.set_data(self.band_image(layer=layer).numpy())

            le = self.layer_energy().detach().cpu().numpy()
            x_history.append(self.state.step_count)
            for li in range(NUM_LAYERS):
                y_history[li].append(float(le[li]))
                lines[li].set_data(x_history, y_history[li])
            ax_energy.set_xlim(0, max(20, self.state.step_count))
            ymax = max(1.0, max((max(v) if v else 0.0) for v in y_history) * 1.1)
            ax_energy.set_ylim(0, ymax)

            r = self.symbolic_readout()
            title.set_text(
                f"RRC-2 Live View | step={self.state.step_count} | coherence={r['global_coherence']:.3f} | "
                f"PE={r['prediction_error_norm']:.3f} | self={r['self_state_norm']:.3f} | dominant={r['dominant_layer']}"
            )
            return [sc, im, *lines, title]

        animation = FuncAnimation(fig, update, frames=frames, interval=interval, blit=False, repeat=False)
        plt.tight_layout()
        plt.show()
        return animation

    # ------------------------------------------------------------------
    # Text summary
    # ------------------------------------------------------------------

    def summary(self) -> str:
        r = self.symbolic_readout()
        return "\n".join([
            f"device={self.device}",
            f"step={self.state.step_count}",
            f"global_coherence={r['global_coherence']:.4f}",
            f"crystal_symmetry={r['crystal_symmetry']:.4f}",
            f"dominant_energy_band={r['dominant_energy_band']} ({r['dominant_energy']:.4f})",
            f"dominant_layer={r['dominant_layer']} ({r['dominant_layer_energy']:.4f})",
            f"world_state_norm={r['world_state_norm']:.4f}",
            f"self_state_norm={r['self_state_norm']:.4f}",
            f"motor_state_norm={r['motor_state_norm']:.4f}",
            f"prediction_error_norm={r['prediction_error_norm']:.4f}",
            "drives=" + ", ".join(f"{k}:{v:.3f}" for k, v in r["drives"].items()),
        ])


def demo() -> None:
    print("Initializing concise GPU RRC-2...\n")
    rrc = RainbowResonanceCortexRRC2(
        RRC2Config(width=40, height=40, dt=0.02, seed=11, coherence_window=12)
    )

    print("Initial state:")
    print(rrc.summary())

    print("\nInject symbolic concept into L4 sensory relay: 'balance'\n")
    rrc.inject_symbol("balance")
    rrc.run(steps=80)
    print(rrc.summary())

    print("\nAutonomous developmental growth...\n")
    rrc.developmental_cycle(steps=120)
    print(rrc.summary())

    print("\nFinal symbolic readout:")
    print(rrc.symbolic_readout())

    # Uncomment to open the live cortex visualization window.
    # rrc.live_visualize(steps_per_frame=2, frames=240, interval=40, mode='amplitude', layer=0)


if __name__ == "__main__":
    demo()
