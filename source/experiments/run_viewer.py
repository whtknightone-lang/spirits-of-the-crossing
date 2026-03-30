"""
Spirits of the Crossing — Live Viewer
======================================
Left panel  : 2D universe map — star, planet orbits, regions, agents
Right panel : 3D brain of the highest-energy agent
              (snowflake nodes coloured by phase + translucent fluid shell)

Run from the source/ directory:
    python experiments/run_viewer.py
"""

import os
import sys

import numpy as np
import matplotlib.pyplot as plt

# ensure source/ is on the path when run directly
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from engine.simulation.universe import Universe
from engine.metrics.sync import sync
from engine.metrics.diversity import diversity
from engine.metrics.energy import mean_energy


def main():
    universe = Universe(num_planets=5, agents_per_planet=16, regions_per_planet=8)

    plt.ion()
    fig = plt.figure(figsize=(13, 6))
    ax_system = fig.add_subplot(1, 2, 1)
    ax_brain = fig.add_subplot(1, 2, 2, projection="3d")

    rng = np.random.default_rng(1234)

    for step_num in range(3500):
        universe.step()

        if step_num % 100 == 0:
            phases = universe.all_phases()
            energies = universe.all_energies()
            myth_lines = "  ".join(
                f"{p.name}[{p.myth_state.summary()}]" for p in universe.planets
            )
            print(
                f"step {step_num:4d} | "
                f"sync {sync(phases):.3f} | "
                f"div {diversity(phases):.3f} | "
                f"avg_energy {mean_energy(energies):.3f} | "
                f"core {universe.mean_core_energy():.3f} | "
                f"shell {universe.mean_shell_coherence():.3f} | "
                f"reward {universe.mean_reward():.3f} | "
                f"penalty {universe.mean_penalty():.3f}"
            )
            print(f"       myths: {myth_lines}")

        # ---- Universe panel ----
        ax_system.clear()
        ax_system.set_title("Spirits of the Crossing — Universe")
        ax_system.scatter([0], [0], s=520, marker="*", color="gold", label="Star")

        for p in universe.planets:
            x, y = p.pos
            orbit = plt.Circle((0, 0), p.radius, fill=False, alpha=0.18)
            ax_system.add_patch(orbit)
            ax_system.scatter([x], [y], s=180)
            # planet label: name + highest active myth tier
            tier = p.myth_state.highest_tier()
            tier_tag = f" ·{tier[0]}" if tier else ""
            ax_system.text(x + 0.12, y + 0.12, f"{p.name}{tier_tag}", fontsize=7)

            for rid in range(p.n_regions):
                rx, ry = p.region_world_pos(rid)
                ax_system.scatter([rx], [ry], s=35, alpha=0.45)

            planet_agents = [a for a in universe.agents if a.planet_id == p.index]
            for a in planet_agents:
                rx, ry = p.region_world_pos(a.region_id)
                jitter = 0.06
                ax_system.scatter(
                    [rx + rng.uniform(-jitter, jitter)],
                    [ry + rng.uniform(-jitter, jitter)],
                    s=18,
                    alpha=0.75,
                )

        # Draw Upsilon nodes — pulsing circles whose size breathes with sin(phase)
        # Colour encodes frequency (low=blue, high=red); alpha encodes amplitude
        freq_values = [n.frequency for n in universe.upsilon_nodes]
        freq_min, freq_max = min(freq_values), max(freq_values)
        for node in universe.upsilon_nodes:
            pulse = abs(np.sin(node.phase))
            radius = max(0.05, node.orbit_r * pulse * node.amplitude * 0.7)
            alpha = 0.15 + 0.45 * pulse
            t = (node.frequency - freq_min) / max(1e-6, freq_max - freq_min)
            colour = (t, 0.3, 1.0 - t)   # blue → red across frequency range
            circle = plt.Circle(node.pos, radius, color=colour, fill=True, alpha=alpha)
            ax_system.add_patch(circle)
            # outer ring shows influence radius faintly
            ring = plt.Circle(node.pos, node.influence_radius,
                              color=colour, fill=False, alpha=0.06, linewidth=0.8)
            ax_system.add_patch(ring)

        lim = max(p.radius for p in universe.planets) + 1.5
        ax_system.set_xlim(-lim, lim)
        ax_system.set_ylim(-lim, lim)
        ax_system.set_aspect("equal", adjustable="box")

        # ---- Brain panel ----
        ax_brain.clear()
        agent = max(universe.agents, key=lambda a: a.energy)
        pos = agent.brain.positions
        colors = agent.brain.phases / (2 * np.pi)

        # shell nodes shown slightly outside the outer layer
        shell_pos = pos[7:21] * 1.15
        shell_colors = agent.brain.shell / (2 * np.pi)

        ax_brain.scatter(
            pos[:, 0], pos[:, 1], pos[:, 2], c=colors, cmap=plt.cm.hsv, s=80
        )
        ax_brain.scatter(
            shell_pos[:, 0],
            shell_pos[:, 1],
            shell_pos[:, 2],
            c=shell_colors,
            cmap=plt.cm.cool,
            s=40,
            alpha=0.5,
        )

        # draw coupling edges
        for i in range(agent.brain.n):
            for j in range(i + 1, agent.brain.n):
                if agent.brain.matrix[i, j] != 0:
                    ax_brain.plot(
                        [pos[i, 0], pos[j, 0]],
                        [pos[i, 1], pos[j, 1]],
                        [pos[i, 2], pos[j, 2]],
                        color="gray",
                        alpha=0.25,
                        linewidth=1,
                    )

        # highlight center node
        ax_brain.scatter([0], [0], [0], s=180, marker="o", color="white", edgecolors="black")
        ax_brain.set_title(
            f"Highest-Energy Brain | E={agent.energy:.2f} | Core={agent.brain.core_energy:.2f}"
        )
        ax_brain.set_xlim([-2, 2])
        ax_brain.set_ylim([-2, 2])
        ax_brain.set_zlim([-2, 2])

        plt.tight_layout()
        plt.pause(0.01)

    plt.ioff()
    plt.show()


if __name__ == "__main__":
    main()
