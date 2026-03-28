"""
RBE-1 Runnable Prototype
========================

Resonance Behavior Engine connected to an AWE-1-style scalable game world.

What this upgraded prototype does
---------------------------------
- simulates a 2D ecological world inspired by AWE-1
- spawns agents with a 7-color vibrational brain
- converts local world inputs into spectral amplitudes
- computes behavior drives with RBE-1
- arbitrates actions: move, attack, consume, signal, flee, idle
- updates a spectral signal field in the world
- adds AWE-1-style terrain, resource, hazard, energy, and ecological patches
- supports simple team ecology and emergent group behavior
- supports a matplotlib live view

This version is still compact and playable, but now much closer to a scalable
AWE-1 game-world structure.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, List, Tuple, Optional
import math
import random
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation


# -----------------------------------------------------------------------------
# Spectral definitions
# -----------------------------------------------------------------------------

BANDS = ["red", "orange", "yellow", "green", "blue", "indigo", "violet"]
B = len(BANDS)
BI = {name: i for i, name in enumerate(BANDS)}


# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------

@dataclass
class WorldConfig:
    width: int = 96
    height: int = 96
    num_agents: int = 64
    food_regen: float = 0.003
    signal_decay: float = 0.05
    signal_diffusion: float = 0.20
    hazard_strength: float = 0.4
    max_steps: int = 5000
    energy_regen: float = 0.002
    resource_patch_gain: float = 0.20
    safe_zone_gain: float = 0.10
    signal_patch_gain: float = 0.15


@dataclass
class AgentConfig:
    max_health: float = 1.0
    max_energy: float = 1.5
    move_cost: float = 0.01
    attack_cost: float = 0.02
    signal_cost: float = 0.01
    idle_recovery: float = 0.004
    consume_gain: float = 0.08
    sight_radius: int = 4
    attack_damage: float = 0.15
    respawn_energy: float = 1.0


# -----------------------------------------------------------------------------
# Data classes
# -----------------------------------------------------------------------------

@dataclass
class BrainState:
    amplitude: np.ndarray = field(default_factory=lambda: np.zeros(B, dtype=np.float32))
    phase: np.ndarray = field(default_factory=lambda: np.zeros(B, dtype=np.float32))
    frequency: np.ndarray = field(default_factory=lambda: np.linspace(0.9, 1.1, B, dtype=np.float32))
    memory_bias: np.ndarray = field(default_factory=lambda: np.zeros(B, dtype=np.float32))
    coherence: float = 0.0


@dataclass
class Agent:
    x: int
    y: int
    dx: int
    dy: int
    team: int
    health: float
    energy: float
    alive: bool
    brain: BrainState
    mode: str = "idle"
    cooldown: int = 0


# -----------------------------------------------------------------------------
# World + RBE-1
# -----------------------------------------------------------------------------

class RBE1Prototype:
    def __init__(self, wcfg: Optional[WorldConfig] = None, acfg: Optional[AgentConfig] = None, seed: int = 7):
        self.wcfg = wcfg or WorldConfig()
        self.acfg = acfg or AgentConfig()
        self.rng = random.Random(seed)
        np.random.seed(seed)

        H, W = self.wcfg.height, self.wcfg.width
        yy, xx = np.mgrid[0:H, 0:W]
        xf = xx / max(1, W - 1)
        yf = yy / max(1, H - 1)

        # AWE-1 style world tensors.
        self.terrain = np.clip(0.5 + 0.25 * np.sin(3 * xf) * np.cos(2 * yf), 0.0, 1.0).astype(np.float32)
        self.food = np.clip(0.55 + 0.25 * np.sin(7 * xf) * np.cos(5 * yf), 0.0, 1.0).astype(np.float32)
        self.hazard = np.clip(0.35 + self.wcfg.hazard_strength * np.cos(4 * xf + 1.2) * np.sin(6 * yf), 0.0, 1.0).astype(np.float32)
        self.energy_field = np.clip(0.5 * self.food + 0.2 * (1.0 - self.hazard), 0.0, 1.0).astype(np.float32)
        self.signal = np.zeros((B, H, W), dtype=np.float32)
        self.step_count = 0

        # Ecological patches for more AWE-like dynamics.
        self.resource_patch = np.clip(0.5 + self.wcfg.resource_patch_gain * np.cos(8 * xf - 1.5) * np.cos(8 * yf + 0.4), 0.0, 1.0).astype(np.float32)
        self.safe_zone = np.clip(0.5 + self.wcfg.safe_zone_gain * np.sin(5 * xf) * np.sin(5 * yf), 0.0, 1.0).astype(np.float32)
        self.signal_patch = np.clip(0.5 + self.wcfg.signal_patch_gain * np.cos(6 * xf) * np.sin(7 * yf), 0.0, 1.0).astype(np.float32)

        self.agents: List[Agent] = []
        for i in range(self.wcfg.num_agents):
            team = i % 2
            brain = BrainState(
                amplitude=np.zeros(B, dtype=np.float32),
                phase=np.random.uniform(0, 2 * math.pi, size=B).astype(np.float32),
                frequency=np.linspace(0.9, 1.1, B).astype(np.float32) + np.random.normal(0, 0.03, size=B).astype(np.float32),
                memory_bias=np.zeros(B, dtype=np.float32),
                coherence=0.0,
            )
            self.agents.append(
                Agent(
                    x=self.rng.randrange(W),
                    y=self.rng.randrange(H),
                    dx=self.rng.choice([-1, 0, 1]),
                    dy=self.rng.choice([-1, 0, 1]),
                    team=team,
                    health=self.acfg.max_health,
                    energy=self.acfg.respawn_energy,
                    alive=True,
                    brain=brain,
                )
            )

        self.metrics: Dict[str, List[float]] = {
            "alive": [],
            "mean_energy": [],
            "mean_health": [],
            "coherence": [],
            "signals": [],
            "food": [],
            "world_energy": [],
            "hazard": [],
            "resource_patch": [],
        }

    # ------------------------------------------------------------------
    # Utilities
    # ------------------------------------------------------------------

    def wrap(self, x: int, y: int) -> Tuple[int, int]:
        return x % self.wcfg.width, y % self.wcfg.height

    def local_mean(self, field: np.ndarray, x: int, y: int, r: int) -> float:
        vals = []
        for oy in range(-r, r + 1):
            for ox in range(-r, r + 1):
                xx, yy = self.wrap(x + ox, y + oy)
                vals.append(field[yy, xx])
        return float(np.mean(vals))

    def local_band_mean(self, field: np.ndarray, x: int, y: int, r: int) -> np.ndarray:
        vals = np.zeros(B, dtype=np.float32)
        count = 0
        for oy in range(-r, r + 1):
            for ox in range(-r, r + 1):
                xx, yy = self.wrap(x + ox, y + oy)
                vals += field[:, yy, xx]
                count += 1
        return vals / max(1, count)

    # ------------------------------------------------------------------
    # Perception -> vibrational brain
    # ------------------------------------------------------------------

    def nearest_enemy_distance(self, agent_index: int) -> float:
        a = self.agents[agent_index]
        best = 1e9
        for j, other in enumerate(self.agents):
            if j == agent_index or not other.alive or other.team == a.team:
                continue
            dx = min(abs(other.x - a.x), self.wcfg.width - abs(other.x - a.x))
            dy = min(abs(other.y - a.y), self.wcfg.height - abs(other.y - a.y))
            d = math.sqrt(dx * dx + dy * dy)
            if d < best:
                best = d
        return best if best < 1e9 else 99.0

    def build_sensory_vector(self, i: int) -> np.ndarray:
        a = self.agents[i]
        r = self.acfg.sight_radius
        food = self.local_mean(self.food, a.x, a.y, r)
        hazard = self.local_mean(self.hazard, a.x, a.y, r)
        terrain = self.local_mean(self.terrain, a.x, a.y, r)
        world_energy = self.local_mean(self.energy_field, a.x, a.y, r)
        resource_patch = self.local_mean(self.resource_patch, a.x, a.y, r)
        safe_zone = self.local_mean(self.safe_zone, a.x, a.y, r)
        signal_patch = self.local_mean(self.signal_patch, a.x, a.y, r)
        signals = self.local_band_mean(self.signal, a.x, a.y, r)
        enemy_dist = self.nearest_enemy_distance(i)
        enemy_near = 1.0 / (1.0 + enemy_dist)
        move_mag = math.sqrt(a.dx * a.dx + a.dy * a.dy)
        safe = 0.5 * (a.health / self.acfg.max_health) + 0.5 * (a.energy / self.acfg.max_energy)
        novelty = float(np.mean(np.abs(a.brain.memory_bias - signals)))

        sensory = np.zeros(B, dtype=np.float32)
        sensory[BI["red"]] = np.clip(0.65 * hazard + 0.65 * enemy_near + 0.20 * signals[BI["red"]], 0.0, 1.0)
        sensory[BI["orange"]] = np.clip(0.25 * terrain + 0.35 * enemy_near + 0.20 * signals[BI["orange"]], 0.0, 1.0)
        sensory[BI["yellow"]] = np.clip(0.55 * food + 0.35 * resource_patch + 0.20 * signals[BI["yellow"]], 0.0, 1.0)
        sensory[BI["green"]] = np.clip(0.45 * safe + 0.25 * safe_zone + 0.20 * world_energy + 0.15 * (1.0 - hazard), 0.0, 1.0)
        sensory[BI["blue"]] = np.clip(0.45 * move_mag + 0.20 * terrain + 0.20 * signals[BI["blue"]], 0.0, 1.0)
        sensory[BI["indigo"]] = np.clip(0.55 * signals[BI["indigo"]] + 0.20 * signal_patch, 0.0, 1.0)
        sensory[BI["violet"]] = np.clip(0.55 * novelty + 0.20 * signal_patch + 0.15 * resource_patch + 0.20 * signals[BI["violet"]], 0.0, 1.0)
        return sensory

    def update_brain(self, i: int, sensory: np.ndarray, dt: float = 0.15) -> None:
        a = self.agents[i]
        b = a.brain

        # Simple snowflake-style coupling: green hub influences all, neighbors influence adjacent bands.
        amp = b.amplitude.copy()
        ph = b.phase.copy()
        mem = b.memory_bias.copy()

        # Phase relations
        new_phase = ph + dt * b.frequency
        hub = BI["green"]
        for k in range(B):
            if k == hub:
                continue
            new_phase[k] += dt * 0.18 * math.sin(ph[hub] - ph[k])
        for k in range(B):
            left = (k - 1) % B
            right = (k + 1) % B
            new_phase[k] += dt * 0.08 * math.sin(ph[left] - ph[k])
            new_phase[k] += dt * 0.08 * math.sin(ph[right] - ph[k])
        new_phase = np.mod(new_phase, 2 * math.pi).astype(np.float32)

        # Amplitude update
        new_amp = amp + dt * (
            0.85 * amp
            - 0.95 * (amp ** 3)
            + 0.90 * sensory
            + 0.25 * mem
            - 0.20 * amp
        )
        new_amp = np.clip(new_amp, 0.0, 1.0).astype(np.float32)

        # Coherence
        z = np.exp(1j * new_phase)
        coherence = float(np.abs(np.mean(z)))

        # Memory bias update
        mem = 0.97 * mem + 0.03 * sensory

        b.phase = new_phase
        b.amplitude = new_amp
        b.memory_bias = np.clip(mem, 0.0, 1.0).astype(np.float32)
        b.coherence = coherence

    # ------------------------------------------------------------------
    # RBE-1 drive computation
    # ------------------------------------------------------------------

    def compute_drives(self, i: int) -> Dict[str, float]:
        a = self.agents[i]
        A = a.brain.amplitude
        P = a.brain.phase
        C = a.brain.coherence
        health_frac = a.health / self.acfg.max_health
        energy_frac = a.energy / self.acfg.max_energy

        p_rb = math.cos(float(P[BI["red"]] - P[BI["blue"]]))
        p_rg = math.cos(float(P[BI["red"]] - P[BI["green"]]))

        aggression = max(0.0, (0.70 * A[BI["red"]] + 0.30 * A[BI["blue"]] - 0.40 * A[BI["green"]])) * (0.5 + 0.5 * max(0.0, p_rb))
        fear = max(0.0, (0.80 * A[BI["red"]] + 0.35 * (1.0 - health_frac) - 0.20 * A[BI["green"]])) * (0.6 + 0.4 * max(0.0, p_rg))
        seek = max(0.0, 0.60 * A[BI["yellow"]] + 0.30 * A[BI["blue"]] + 0.20 * A[BI["violet"]] + 0.25 * (1.0 - energy_frac))
        rest = max(0.0, 0.70 * A[BI["green"]] - 0.30 * A[BI["red"]] - 0.20 * A[BI["violet"]] + 0.30 * (1.0 - health_frac))
        social = max(0.0, 0.70 * A[BI["indigo"]] + 0.20 * A[BI["orange"]])
        explore = max(0.0, 0.80 * A[BI["violet"]] + 0.30 * A[BI["blue"]] - 0.20 * A[BI["green"]])

        # Coherence scales commitment.
        aggression *= C
        fear *= C
        seek *= max(0.35, C)
        rest *= max(0.35, C)
        social *= max(0.35, C)
        explore *= max(0.35, C)

        return {
            "attack": float(aggression),
            "flee": float(fear),
            "seek": float(seek),
            "rest": float(rest),
            "signal": float(social),
            "explore": float(explore),
        }

    def arbitrate(self, drives: Dict[str, float], cooldown: int) -> str:
        weights = drives.copy()
        if cooldown > 0:
            weights["attack"] *= 0.5
        return max(weights.items(), key=lambda kv: kv[1])[0]

    # ------------------------------------------------------------------
    # Action execution
    # ------------------------------------------------------------------

    def move_toward_best_food(self, a: Agent) -> None:
        best_score = -1.0
        best = (a.x, a.y)
        r = self.acfg.sight_radius
        for oy in range(-r, r + 1):
            for ox in range(-r, r + 1):
                xx, yy = self.wrap(a.x + ox, a.y + oy)
                score = float(self.food[yy, xx] - 0.4 * self.hazard[yy, xx])
                if score > best_score:
                    best_score = score
                    best = (xx, yy)
        dx = int(np.sign(best[0] - a.x))
        dy = int(np.sign(best[1] - a.y))
        a.dx, a.dy = dx, dy

    def move_away_from_hazard(self, a: Agent) -> None:
        best_score = -1e9
        best = (a.x, a.y)
        for oy in (-1, 0, 1):
            for ox in (-1, 0, 1):
                xx, yy = self.wrap(a.x + ox, a.y + oy)
                score = float(-self.hazard[yy, xx] + 0.2 * self.food[yy, xx])
                if score > best_score:
                    best_score = score
                    best = (xx, yy)
        a.dx = int(np.sign(best[0] - a.x))
        a.dy = int(np.sign(best[1] - a.y))

    def random_walk(self, a: Agent) -> None:
        a.dx = self.rng.choice([-1, 0, 1])
        a.dy = self.rng.choice([-1, 0, 1])

    def emit_signal(self, a: Agent, strength_scale: float = 0.25) -> None:
        signal_vec = strength_scale * a.brain.amplitude
        self.signal[:, a.y, a.x] = np.clip(self.signal[:, a.y, a.x] + signal_vec, 0.0, 1.0)
        a.energy = max(0.0, a.energy - self.acfg.signal_cost)

    def attack_enemy(self, i: int) -> None:
        a = self.agents[i]
        target = None
        best = 99.0
        for j, other in enumerate(self.agents):
            if j == i or not other.alive or other.team == a.team:
                continue
            dx = min(abs(other.x - a.x), self.wcfg.width - abs(other.x - a.x))
            dy = min(abs(other.y - a.y), self.wcfg.height - abs(other.y - a.y))
            d = math.sqrt(dx * dx + dy * dy)
            if d < best:
                best = d
                target = other
        if target is None:
            return
        if best <= 1.5:
            target.health -= self.acfg.attack_damage
            a.energy = max(0.0, a.energy - self.acfg.attack_cost)
            a.cooldown = 4
            if target.health <= 0.0:
                target.alive = False
        else:
            a.dx = int(np.sign(target.x - a.x))
            a.dy = int(np.sign(target.y - a.y))

    def consume(self, a: Agent) -> None:
        available = self.food[a.y, a.x]
        taken = min(float(available), self.acfg.consume_gain)
        self.food[a.y, a.x] = max(0.0, self.food[a.y, a.x] - taken)
        a.energy = min(self.acfg.max_energy, a.energy + taken)
        a.health = min(self.acfg.max_health, a.health + 0.35 * taken)

    def do_action(self, i: int) -> None:
        a = self.agents[i]
        if not a.alive:
            return

        drives = self.compute_drives(i)
        mode = self.arbitrate(drives, a.cooldown)
        a.mode = mode

        if mode == "seek":
            self.move_toward_best_food(a)
            self.consume(a)
        elif mode == "flee":
            self.move_away_from_hazard(a)
            self.emit_signal(a, strength_scale=0.20)
        elif mode == "attack":
            self.attack_enemy(i)
        elif mode == "signal":
            self.emit_signal(a, strength_scale=0.30)
        elif mode == "explore":
            self.random_walk(a)
        elif mode == "rest":
            a.dx, a.dy = 0, 0
            a.energy = min(self.acfg.max_energy, a.energy + self.acfg.idle_recovery)
            a.health = min(self.acfg.max_health, a.health + 0.5 * self.acfg.idle_recovery)
        else:
            a.dx, a.dy = 0, 0

    # ------------------------------------------------------------------
    # World update
    # ------------------------------------------------------------------

    def update_world(self) -> None:
        self.food = np.clip(self.food + self.wcfg.food_regen * (1.0 - self.food), 0.0, 1.0)

        # Diffuse + decay signals.
        s = self.signal
        self.signal = (
            (1.0 - self.wcfg.signal_diffusion) * s
            + self.wcfg.signal_diffusion * 0.25 * (
                np.roll(s, 1, axis=1)
                + np.roll(s, -1, axis=1)
                + np.roll(s, 1, axis=2)
                + np.roll(s, -1, axis=2)
            )
        )
        self.signal *= (1.0 - self.wcfg.signal_decay)
        self.signal = np.clip(self.signal, 0.0, 1.0)

        # Move agents + metabolism + respawn.
        for a in self.agents:
            if a.cooldown > 0:
                a.cooldown -= 1
            if a.alive:
                a.x, a.y = self.wrap(a.x + a.dx, a.y + a.dy)
                a.energy = max(0.0, a.energy - self.acfg.move_cost * float(abs(a.dx) + abs(a.dy)))
                a.energy = max(0.0, a.energy - 0.003)
                a.health = max(0.0, a.health - 0.02 * float(self.hazard[a.y, a.x]))
                if a.energy <= 0.0 or a.health <= 0.0:
                    a.alive = False

            if not a.alive:
                # Respawn with softened memory.
                a.x = self.rng.randrange(self.wcfg.width)
                a.y = self.rng.randrange(self.wcfg.height)
                a.dx = self.rng.choice([-1, 0, 1])
                a.dy = self.rng.choice([-1, 0, 1])
                a.health = self.acfg.max_health
                a.energy = self.acfg.respawn_energy
                a.alive = True
                a.brain.memory_bias *= 0.85
                a.brain.amplitude *= 0.25
                a.mode = "respawn"

    # ------------------------------------------------------------------
    # Simulation
    # ------------------------------------------------------------------

    def step(self) -> None:
        for i, a in enumerate(self.agents):
            sensory = self.build_sensory_vector(i)
            self.update_brain(i, sensory)
        for i, _ in enumerate(self.agents):
            self.do_action(i)
        self.update_world()
        self.step_count += 1
        self.record_metrics()

    def run(self, steps: int = 200) -> None:
        for _ in range(steps):
            self.step()

    # ------------------------------------------------------------------
    # Metrics and visualization
    # ------------------------------------------------------------------

    def record_metrics(self) -> None:
        alive = sum(1 for a in self.agents if a.alive)
        mean_energy = float(np.mean([a.energy for a in self.agents]))
        mean_health = float(np.mean([a.health for a in self.agents]))
        coherence = float(np.mean([a.brain.coherence for a in self.agents]))
        signal_level = float(np.mean(self.signal))
        food_level = float(np.mean(self.food))
        world_energy_level = float(np.mean(self.energy_field))
        hazard_level = float(np.mean(self.hazard))
        resource_patch_level = float(np.mean(self.resource_patch))
        self.metrics["alive"].append(alive)
        self.metrics["mean_energy"].append(mean_energy)
        self.metrics["mean_health"].append(mean_health)
        self.metrics["coherence"].append(coherence)
        self.metrics["signals"].append(signal_level)
        self.metrics["food"].append(food_level)
        self.metrics["world_energy"].append(world_energy_level)
        self.metrics["hazard"].append(hazard_level)
        self.metrics["resource_patch"].append(resource_patch_level)

    def world_rgb(self) -> np.ndarray:
        r = np.clip(self.hazard + 0.8 * self.signal[BI["red"]], 0.0, 1.0)
        g = np.clip(self.food + 0.4 * self.signal[BI["green"]], 0.0, 1.0)
        b = np.clip(self.signal[BI["blue"]] + 0.7 * self.signal[BI["violet"]], 0.0, 1.0)
        img = np.stack([r, g, b], axis=-1)
        for a in self.agents:
            color = np.array([1.0, 1.0, 1.0]) if a.team == 0 else np.array([1.0, 0.5, 0.5])
            img[a.y, a.x] = color
        return img

    def agent_brain_image(self, index: int = 0) -> np.ndarray:
        a = self.agents[index]
        vals = a.brain.amplitude.astype(np.float32)
        return np.tile(vals[None, :], (12, 1))

    def live_view(self, steps_per_frame: int = 2, frames: int = 300, interval: int = 50) -> None:
        fig = plt.figure(figsize=(13, 6))
        gs = fig.add_gridspec(1, 3, width_ratios=[1.2, 0.8, 1.0])
        ax_world = fig.add_subplot(gs[0, 0])
        ax_brain = fig.add_subplot(gs[0, 1])
        ax_metrics = fig.add_subplot(gs[0, 2])

        world_im = ax_world.imshow(self.world_rgb(), animated=True)
        ax_world.set_title("RBE-1 World")
        ax_world.set_xticks([])
        ax_world.set_yticks([])

        brain_im = ax_brain.imshow(self.agent_brain_image(0), aspect="auto", animated=True)
        ax_brain.set_title("Agent 0 Spectral Brain")
        ax_brain.set_xticks(range(B))
        ax_brain.set_xticklabels(BANDS, rotation=45, ha="right")
        ax_brain.set_yticks([])

        lines = {}
        for key in ("mean_energy", "coherence", "signals"):
            line, = ax_metrics.plot([], [], label=key)
            lines[key] = line
        ax_metrics.legend(loc="upper left", fontsize=8)
        ax_metrics.set_title("Metrics")

        title = fig.suptitle("RBE-1 Runnable Prototype", fontsize=14)

        def update(_frame: int):
            for _ in range(steps_per_frame):
                self.step()
            world_im.set_data(self.world_rgb())
            brain_im.set_data(self.agent_brain_image(0))

            xs = list(range(len(self.metrics["mean_energy"])))
            for key in ("mean_energy", "coherence", "signals"):
                lines[key].set_data(xs, self.metrics[key])
            ax_metrics.set_xlim(0, max(20, len(xs)))
            ymax = max(1.0, max(
                max(self.metrics["mean_energy"][-50:] or [0.0]),
                max(self.metrics["coherence"][-50:] or [0.0]),
                max(self.metrics["signals"][-50:] or [0.0]),
            ))
            ax_metrics.set_ylim(0, ymax * 1.1)

            title.set_text(
                f"RBE-1 | step={self.step_count} | alive={self.metrics['alive'][-1] if self.metrics['alive'] else len(self.agents)} | "
                f"energy={self.metrics['mean_energy'][-1] if self.metrics['mean_energy'] else 0:.3f}"
            )
            return [world_im, brain_im, *lines.values(), title]

        animation = FuncAnimation(fig, update, frames=frames, interval=interval, blit=False, repeat=False)
        plt.tight_layout()
        plt.show()
        return animation

    def summary(self) -> str:
        if not self.metrics["alive"]:
            return f"step=0\nagents={len(self.agents)}"
        return "\n".join([
            f"step={self.step_count}",
            f"agents={len(self.agents)}",
            f"alive={self.metrics['alive'][-1]}",
            f"mean_energy={self.metrics['mean_energy'][-1]:.4f}",
            f"mean_health={self.metrics['mean_health'][-1]:.4f}",
            f"coherence={self.metrics['coherence'][-1]:.4f}",
            f"signal_level={self.metrics['signals'][-1]:.4f}",
            f"food_level={self.metrics['food'][-1]:.4f}",
        ])


def demo() -> None:
    sim = RBE1Prototype(WorldConfig(width=64, height=64, num_agents=36), AgentConfig(), seed=11)
    print("Initial state:")
    print(sim.summary())
    print("\nRunning 200 steps...\n")
    sim.run(200)
    print(sim.summary())

    # Uncomment for live view.
    # _anim = sim.live_view(steps_per_frame=2, frames=300, interval=50)


if __name__ == "__main__":
    demo()
