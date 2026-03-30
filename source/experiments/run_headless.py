"""
Spirits of the Crossing — Headless Snapshot Runner
====================================================
Runs the simulation for N steps and saves PNG snapshots to output/.
No display needed — works on any machine, server, or CI.

Usage:
    python experiments/run_headless.py [--steps 1000] [--snapshots 3]

Output files:
    output/snapshot_step_XXXX.png   — universe + brain panels at each snapshot
    output/final_state.png          — full 4-panel summary at end of run
"""

import os
import sys
import argparse

import matplotlib
matplotlib.use("Agg")   # non-interactive backend — no display required
import matplotlib.pyplot as plt
import matplotlib.gridspec as gridspec
import numpy as np

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from engine.simulation.universe import Universe
from engine.metrics.sync import sync
from engine.metrics.diversity import diversity
from engine.metrics.energy import mean_energy

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "output")


# -----------------------------------------------------------------------
# Rendering helpers
# -----------------------------------------------------------------------

ELEMENT_COLOURS = {
    "earth":   "#5a8c3e",
    "air":     "#7fb3d3",
    "source":  "#b39ddb",
    "water":   "#4fc3f7",
    "machine": "#90a4ae",
    "fire":    "#ef6c00",
}

TIER_MARKERS = {"seedling": "·s", "explorer": "·e", "voyager": "·V"}


def draw_universe(ax, universe, rng):
    ax.set_facecolor("#0a0a0f")
    ax.scatter([0], [0], s=600, marker="*", color="#ffe066", zorder=5)

    # upsilon nodes
    freq_values = [n.frequency for n in universe.upsilon_nodes]
    fmin, fmax = min(freq_values), max(freq_values)
    for node in universe.upsilon_nodes:
        pulse = abs(np.sin(node.phase))
        radius = max(0.05, node.orbit_r * pulse * node.amplitude * 0.7)
        t = (node.frequency - fmin) / max(1e-6, fmax - fmin)
        colour = (t, 0.3, 1.0 - t)
        ax.add_patch(plt.Circle(node.pos, radius, color=colour, alpha=0.25 + 0.4 * pulse))
        ax.add_patch(plt.Circle(node.pos, node.influence_radius,
                                color=colour, fill=False, alpha=0.06, linewidth=0.8))

    for p in universe.planets:
        x, y = p.pos
        elem_col = ELEMENT_COLOURS.get(p.element, "#ffffff")
        ax.add_patch(plt.Circle((0, 0), p.radius, fill=False,
                                color=elem_col, alpha=0.12, linewidth=0.8))
        ax.scatter([x], [y], s=200, color=elem_col, zorder=4)

        tier = p.myth_state.highest_tier()
        tier_tag = TIER_MARKERS.get(tier, "") if tier else ""
        ax.text(x + 0.15, y + 0.15, f"{p.name}{tier_tag}",
                fontsize=7, color=elem_col, fontweight="bold")

        planet_agents = [a for a in universe.agents if a.planet_id == p.index]
        for a in planet_agents:
            rx, ry = p.region_world_pos(a.region_id)
            jitter = 0.06
            ax.scatter(
                [rx + rng.uniform(-jitter, jitter)],
                [ry + rng.uniform(-jitter, jitter)],
                s=12, color=elem_col, alpha=0.55, zorder=3,
            )

    lim = max(p.radius for p in universe.planets) + 1.5
    ax.set_xlim(-lim, lim)
    ax.set_ylim(-lim, lim)
    ax.set_aspect("equal", adjustable="box")
    ax.set_title("Spirits of the Crossing — Universe", color="white", fontsize=9)
    ax.tick_params(colors="grey")
    for spine in ax.spines.values():
        spine.set_edgecolor("#333")


def draw_brain(ax, agent):
    pos = agent.brain.positions
    colors = agent.brain.phases / (2 * np.pi)
    shell_pos = pos[7:21] * 1.15
    shell_colors = agent.brain.shell / (2 * np.pi)

    ax.scatter(pos[:, 0], pos[:, 1], pos[:, 2], c=colors, cmap=plt.cm.hsv, s=80)
    ax.scatter(shell_pos[:, 0], shell_pos[:, 1], shell_pos[:, 2],
               c=shell_colors, cmap=plt.cm.cool, s=35, alpha=0.5)

    for i in range(agent.brain.n):
        for j in range(i + 1, agent.brain.n):
            if agent.brain.matrix[i, j] != 0:
                ax.plot([pos[i, 0], pos[j, 0]],
                        [pos[i, 1], pos[j, 1]],
                        [pos[i, 2], pos[j, 2]],
                        color="white", alpha=0.12, linewidth=0.8)

    ax.scatter([0], [0], [0], s=160, marker="o", color="white", edgecolors="#888", zorder=5)
    ax.set_xlim([-2, 2]); ax.set_ylim([-2, 2]); ax.set_zlim([-2, 2])
    ax.set_facecolor("#0a0a0f")
    p_name = next((p.name for p in _universe_ref.planets
                   if p.index == agent.planet_id), "?")
    ax.set_title(
        f"{agent.archetype} @ {p_name}\n"
        f"E={agent.energy:.1f}  core={agent.brain.core_energy:.2f}",
        color="white", fontsize=8,
    )
    ax.tick_params(colors="grey")


def draw_myths(ax, universe):
    ax.set_facecolor("#0a0a0f")
    ax.set_title("Active Myths", color="white", fontsize=9)

    y = 0
    for p in universe.planets:
        elem_col = ELEMENT_COLOURS.get(p.element, "#ffffff")
        active = sorted(p.myth_state.active)
        if not active:
            ax.text(0.02, y, f"{p.name}: quiet", color="#555", fontsize=7,
                    transform=ax.transAxes, va="center")
        else:
            parts = [f"{k}({p.myth_state.tiers[k][0]})" for k in active]
            ax.text(0.02, y, f"{p.name}: " + "  ".join(parts),
                    color=elem_col, fontsize=7, transform=ax.transAxes,
                    va="center", fontweight="bold")
        y += 0.16

    ax.axis("off")


def draw_metrics(ax, history):
    ax.set_facecolor("#0a0a0f")
    steps = [h[0] for h in history]
    ax.plot(steps, [h[1] for h in history], color="#ffe066", linewidth=1.2, label="sync")
    ax.plot(steps, [h[2] for h in history], color="#7fb3d3", linewidth=1.2, label="shell")
    ax.set_title("Metrics timeline", color="white", fontsize=9)
    ax.legend(fontsize=7, labelcolor="white", facecolor="#111")
    ax.set_facecolor("#0a0a0f")
    ax.tick_params(colors="grey")
    for spine in ax.spines.values():
        spine.set_edgecolor("#333")


# -----------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------

_universe_ref = None   # used in draw_brain for planet name lookup


def save_snapshot(universe, step, history, rng, label="snapshot"):
    fig = plt.figure(figsize=(16, 9), facecolor="#0a0a0f")
    gs = gridspec.GridSpec(2, 2, figure=fig, hspace=0.35, wspace=0.25)

    ax_uni   = fig.add_subplot(gs[0, 0])
    ax_brain = fig.add_subplot(gs[0, 1], projection="3d")
    ax_myth  = fig.add_subplot(gs[1, 0])
    ax_met   = fig.add_subplot(gs[1, 1])

    draw_universe(ax_uni, universe, rng)

    agent = max(universe.agents, key=lambda a: a.energy)
    draw_brain(ax_brain, agent)
    draw_myths(ax_myth, universe)
    if history:
        draw_metrics(ax_met, history)
    else:
        ax_met.axis("off")

    phases = universe.all_phases()
    fig.suptitle(
        f"Spirits of the Crossing  |  step {step}  |  "
        f"sync {sync(phases):.3f}  |  avg_energy {mean_energy(universe.all_energies()):.1f}",
        color="white", fontsize=10, y=0.98,
    )

    os.makedirs(OUTPUT_DIR, exist_ok=True)
    path = os.path.join(OUTPUT_DIR, f"{label}_step_{step:05d}.png")
    fig.savefig(path, dpi=150, bbox_inches="tight", facecolor=fig.get_facecolor())
    plt.close(fig)
    print(f"  saved → {path}")


def run(total_steps: int = 1000, n_snapshots: int = 4):
    global _universe_ref
    universe = Universe(agents_per_planet=16, regions_per_planet=8)
    _universe_ref = universe

    rng = np.random.default_rng(42)
    history = []
    snapshot_every = max(1, total_steps // n_snapshots)

    print(f"\nSpirits of the Crossing — Headless run ({total_steps} steps)\n")
    print("Worlds:")
    for p in universe.planets:
        print(f"  {p.name:14s} ({p.element:8s})  upsilon={p.lore.upsilon_frequency:.2f} Hz")
    print()

    for step in range(1, total_steps + 1):
        universe.step()

        phases = universe.all_phases()
        history.append((
            step,
            sync(phases),
            universe.mean_shell_coherence(),
            mean_energy(universe.all_energies()),
        ))

        if step % 100 == 0:
            myth_line = "  ".join(
                f"{p.name}[{p.myth_state.summary()}]" for p in universe.planets
            )
            print(
                f"step {step:5d} | "
                f"sync {sync(phases):.3f} | "
                f"energy {mean_energy(universe.all_energies()):.1f} | "
                f"core {universe.mean_core_energy():.3f}"
            )
            print(f"         myths: {myth_line}")

        if step % snapshot_every == 0 or step == total_steps:
            save_snapshot(universe, step, history, rng)

    print(f"\nDone. Snapshots saved to: {os.path.abspath(OUTPUT_DIR)}/")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Run Spirits of the Crossing headless")
    parser.add_argument("--steps",     type=int, default=1000,
                        help="Total simulation steps (default: 1000)")
    parser.add_argument("--snapshots", type=int, default=4,
                        help="Number of PNG snapshots to save (default: 4)")
    args = parser.parse_args()
    run(total_steps=args.steps, n_snapshots=args.snapshots)
