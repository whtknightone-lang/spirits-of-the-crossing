#!/usr/bin/env python3
"""
Bootstrap script for the Resonance Universe starter repository.

Usage:
    python bootstrap_resonance_universe.py

What it does:
- creates the full repository structure
- writes starter source files
- writes requirements.txt and README.md
- optionally installs dependencies
- optionally runs the starter simulation

This is a practical scaffold, not the full research platform.
"""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path
from textwrap import dedent


ROOT = Path.cwd() / "resonance-universe"


FILES = {
    "README.md": dedent("""\
        # Resonance Universe

        A starter research repository for oscillator-based cognition,
        fractal snowflake brains, resonance reward, and artificial life simulation.

        ## Quick start

        ```bash
        python bootstrap_resonance_universe.py
        cd resonance-universe
        python run_simulation.py
        ```

        ## Repository layout

        - `engine/` - oscillator engine and plasticity
        - `brain/` - snowflake and fractal brain builders
        - `genome/` - resonance genome and compiler
        - `ecosystem/` - world, creature, and simulation loop
        - `visualization/` - basic plots and displays
        - `experiments/` - starter experiment entry points
        - `tests/` - simple smoke tests
        - `docs/` - theory and architecture notes
        """),

    "requirements.txt": dedent("""\
        numpy
        torch
        pygame
        matplotlib
        networkx
        pandas
        scipy
        """),

    "setup.py": dedent("""\
        from setuptools import setup, find_packages

        setup(
            name="resonance_universe",
            version="0.1.0",
            packages=find_packages(),
            install_requires=[
                "numpy",
                "torch",
                "pygame",
                "matplotlib",
                "networkx",
                "pandas",
                "scipy",
            ],
        )
        """),

    "run_simulation.py": dedent("""\
        from engine.resonance_engine import ResonanceEngine
        from engine.plasticity import update_weights
        from genome.genome import Genome
        from genome.genome_compiler import compile_brain
        from ecosystem.world import World
        from ecosystem.creature import Creature
        from ecosystem.simulation_loop import step
        from visualization.world_view import save_world
        from visualization.phase_wheel import save_phase_wheel

        def main():
            engine = ResonanceEngine(200)
            world = World(size=32)

            genome = Genome()
            brain = compile_brain(genome)
            creature = Creature(genome, brain)

            creatures = [creature]

            reward = 0.0
            for _ in range(100):
                step(world, creatures, engine)
                engine.K = update_weights(engine.K, engine.theta, reward, eta=0.001, decay=0.0001)
                reward = 0.01

            save_world(world, "world_snapshot.png")
            save_phase_wheel(engine.theta, "phase_wheel.png")
            print("Simulation complete.")
            print("Artifacts written: world_snapshot.png, phase_wheel.png")

        if __name__ == "__main__":
            main()
        """),

    "docs/theory.md": dedent("""\
        # Resonance Universe Theory

        Core idea:
        intelligence-like behavior emerges from coupled oscillators
        interacting with world fields under plastic learning.
        """),

    "docs/architecture.md": dedent("""\
        # Architecture

        Unified Resonance Engine
        -> Fractal Snowflake Brain
        -> Resonance Genome Compiler
        -> Ecosystem Simulator
        -> Visualization + Analysis
        """),

    "docs/math_foundations.md": dedent("""\
        # Math Foundations

        Canonical phase equation:
        dtheta_i/dt = omega_i + sum_j K_ij sin(theta_j - theta_i) + I_i

        Plasticity:
        dK_ij/dt = eta cos(theta_i - theta_j) R(t) - mu K_ij
        """),

    "engine/__init__.py": "",
    "engine/resonance_engine.py": dedent("""\
        import torch

        class ResonanceEngine:
            def __init__(self, N, device=None):
                if device is None:
                    if torch.cuda.is_available():
                        device = "cuda"
                    else:
                        device = "cpu"

                self.device = device
                self.N = N
                self.theta = torch.rand(N, device=device) * 2 * torch.pi
                self.omega = torch.normal(1.0, 0.05, (N,), device=device)
                self.K = torch.randn(N, N, device=device) * 0.01
                self.inputs = torch.zeros(N, device=device)
                self.dt = 0.01

            def step(self):
                phase_diff = self.theta.unsqueeze(1) - self.theta.unsqueeze(0)
                coupling = torch.sum(self.K * torch.sin(phase_diff), dim=1)
                self.theta = self.theta + self.dt * (self.omega + coupling + self.inputs)
                return self.theta
        """),

    "engine/gpu_backend.py": dedent("""\
        import torch

        def pick_device():
            return "cuda" if torch.cuda.is_available() else "cpu"
        """),

    "engine/oscillator.py": dedent("""\
        class Oscillator:
            def __init__(self, freq=1.0, phase=0.0):
                self.freq = freq
                self.phase = phase
        """),

    "engine/plasticity.py": dedent("""\
        import torch

        def update_weights(K, theta, reward, eta=0.01, decay=0.001):
            phase_diff = theta.unsqueeze(1) - theta.unsqueeze(0)
            K = K + eta * torch.cos(phase_diff) * reward
            K = K - decay * K
            return K
        """),

    "brain/__init__.py": "",
    "brain/snowflake_core.py": dedent("""\
        import networkx as nx

        def create_snowflake():
            G = nx.Graph()
            nodes = ["I", "C", "P0", "P1", "M0", "E", "M1"]
            G.add_nodes_from(nodes)
            edges = [
                ("I", "C"),
                ("C", "P0"), ("C", "P1"),
                ("C", "M0"), ("C", "E"), ("C", "M1"),
                ("P0", "M0"),
                ("P1", "M1"),
            ]
            G.add_edges_from(edges)
            return G
        """),

    "brain/fractal_brain.py": dedent("""\
        from .snowflake_core import create_snowflake

        def build_fractal(level):
            if level == 0:
                return create_snowflake()
            return [build_fractal(level - 1) for _ in range(7)]
        """),

    "brain/desire_layer.py": dedent("""\
        class DesireLayer:
            def __init__(self):
                self.desires = {
                    "hunger": 2.4,
                    "safety": 1.2,
                    "curiosity": 3.5,
                    "social": 2.8,
                }
        """),

    "genome/__init__.py": "",
    "genome/genome.py": dedent("""\
        class Genome:
            def __init__(self):
                self.snowflake_level = 1
                self.freq_scale = 1.0
                self.desires = {
                    "hunger": 2.4,
                    "safety": 1.2,
                    "curiosity": 3.5,
                    "social": 2.8,
                }
                self.plasticity_gain = 0.01
        """),

    "genome/genome_compiler.py": dedent("""\
        from brain.fractal_brain import build_fractal

        def compile_brain(genome):
            return build_fractal(genome.snowflake_level)
        """),

    "genome/mutation.py": dedent("""\
        import random

        def mutate(genome):
            if random.random() < 0.1:
                genome.freq_scale += random.gauss(0.0, 0.05)
            return genome
        """),

    "ecosystem/__init__.py": "",
    "ecosystem/world.py": dedent("""\
        import numpy as np

        class World:
            def __init__(self, size=32):
                self.size = size
                self.food = np.random.rand(size, size)
                self.danger = np.random.rand(size, size) * 0.5
        """),

    "ecosystem/creature.py": dedent("""\
        class Creature:
            def __init__(self, genome, brain):
                self.genome = genome
                self.brain = brain
                self.energy = 10.0
                self.position = [0, 0]
        """),

    "ecosystem/simulation_loop.py": dedent("""\
        def step(world, creatures, engine):
            engine.step()
            for creature in creatures:
                creature.energy -= 0.01
                creature.position[0] = min(world.size - 1, creature.position[0] + 1)
        """),

    "visualization/__init__.py": "",
    "visualization/world_view.py": dedent("""\
        import matplotlib.pyplot as plt

        def show_world(world):
            plt.imshow(world.food)
            plt.show()

        def save_world(world, path):
            plt.figure()
            plt.imshow(world.food)
            plt.title("World Food Field")
            plt.colorbar()
            plt.savefig(path, bbox_inches="tight")
            plt.close()
        """),

    "visualization/brain_graph.py": dedent("""\
        import matplotlib.pyplot as plt
        import networkx as nx

        def save_brain_graph(G, path):
            plt.figure()
            pos = nx.spring_layout(G, seed=42)
            nx.draw(G, pos, with_labels=True)
            plt.savefig(path, bbox_inches="tight")
            plt.close()
        """),

    "visualization/phase_wheel.py": dedent("""\
        import matplotlib.pyplot as plt
        import numpy as np

        def phase_wheel(theta):
            angles = theta.detach().cpu().numpy()
            plt.polar(angles, np.ones_like(angles), "o")
            plt.show()

        def save_phase_wheel(theta, path):
            angles = theta.detach().cpu().numpy()
            plt.figure()
            plt.polar(angles, np.ones_like(angles), "o")
            plt.title("Oscillator Phase Wheel")
            plt.savefig(path, bbox_inches="tight")
            plt.close()
        """),

    "visualization/metrics_dashboard.py": dedent("""\
        def summarize(creatures):
            return {
                "population": len(creatures),
                "avg_energy": sum(c.energy for c in creatures) / max(1, len(creatures)),
            }
        """),

    "analysis/__init__.py": "",
    "analysis/attractor_analysis.py": dedent("""\
        import numpy as np

        def coherence(theta):
            phases = theta.detach().cpu().numpy()
            return float(np.abs(np.mean(np.exp(1j * phases))))
        """),

    "analysis/population_metrics.py": dedent("""\
        def average_energy(creatures):
            if not creatures:
                return 0.0
            return sum(c.energy for c in creatures) / len(creatures)
        """),

    "experiments/minimal_learner.py": dedent("""\
        from engine.resonance_engine import ResonanceEngine

        def main():
            engine = ResonanceEngine(4)
            for _ in range(10):
                engine.step()
            print("Minimal learner ran successfully.")

        if __name__ == "__main__":
            main()
        """),

    "experiments/snowflake_cognition_test.py": dedent("""\
        from brain.snowflake_core import create_snowflake

        def main():
            G = create_snowflake()
            print("Snowflake nodes:", list(G.nodes()))

        if __name__ == "__main__":
            main()
        """),

    "experiments/ecosystem_evolution.py": dedent("""\
        print("Placeholder for ecosystem evolution experiment.")
        """),

    "tests/test_engine.py": dedent("""\
        from engine.resonance_engine import ResonanceEngine

        def test_engine():
            engine = ResonanceEngine(10)
            engine.step()
            assert engine.theta.shape[0] == 10
        """),

    "tests/test_brain.py": dedent("""\
        from brain.snowflake_core import create_snowflake

        def test_snowflake():
            G = create_snowflake()
            assert len(G.nodes()) == 7
        """),

    "tests/test_genome.py": dedent("""\
        from genome.genome import Genome

        def test_genome():
            genome = Genome()
            assert genome.snowflake_level >= 0
        """),
}


def write_file(rel_path: str, content: str) -> None:
    path = ROOT / rel_path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def write_repo() -> None:
    ROOT.mkdir(parents=True, exist_ok=True)
    for rel_path, content in FILES.items():
        write_file(rel_path, content)


def maybe_install_requirements() -> None:
    answer = input("Install dependencies with pip now? [y/N]: ").strip().lower()
    if answer != "y":
        return
    req = ROOT / "requirements.txt"
    subprocess.run([sys.executable, "-m", "pip", "install", "-r", str(req)], check=False)


def maybe_run_simulation() -> None:
    answer = input("Run starter simulation now? [y/N]: ").strip().lower()
    if answer != "y":
        return
    subprocess.run([sys.executable, "run_simulation.py"], cwd=ROOT, check=False)


def main() -> None:
    print(f"Creating repository at: {ROOT}")
    write_repo()
    print("Repository written successfully.")
    maybe_install_requirements()
    maybe_run_simulation()
    print("Done.")


if __name__ == "__main__":
    main()
