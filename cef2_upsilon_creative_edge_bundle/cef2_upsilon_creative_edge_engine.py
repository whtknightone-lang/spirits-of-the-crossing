
#!/usr/bin/env python3
"""
cef2_upsilon_creative_edge_engine.py

Runnable prototype of a CEF-2 Upsilon Creative Edge Engine.
- 7-channel upsilon nodes
- node identity core
- creative-edge control loop
- biometrics modulation layer for VR skin / wearable / assistive-device exploration

Dependencies:
    pip install numpy matplotlib

Usage:
    python cef2_upsilon_creative_edge_engine.py
    python cef2_upsilon_creative_edge_engine.py --steps 900 --nodes 24
    python cef2_upsilon_creative_edge_engine.py --biometrics path/to/biometrics.csv

Expected CSV columns (optional):
    t, hrv, eda, emg, resp, imu, temp
All channels are normalized or loosely scaled:
    hrv  : higher = calmer / more recovery capacity
    eda  : higher = more sympathetic arousal
    emg  : higher = more effort / muscular intent
    resp : higher = faster breathing; lower + smooth = calmer
    imu  : higher = more motion / agitation / activity
    temp : skin temp delta or normalized thermal comfort proxy
"""

from __future__ import annotations

import argparse
import csv
import math
from dataclasses import dataclass
from pathlib import Path

import numpy as np
import matplotlib.pyplot as plt


CHANNEL_NAMES = [
    "purple_source",
    "blue_memory",
    "green_growth",
    "yellow_attention",
    "orange_desire",
    "red_action",
    "silver_contrast",
]


def clamp(x, lo=0.0, hi=1.0):
    return np.minimum(np.maximum(x, lo), hi)


def sigmoid(x):
    return 1.0 / (1.0 + np.exp(-x))


@dataclass
class BiometricsFrame:
    hrv: float
    eda: float
    emg: float
    resp: float
    imu: float
    temp: float


class BiometricsStream:
    """
    Produces either:
    - synthetic biometrics for rapid prototyping, or
    - user-provided biometrics from CSV
    """

    def __init__(self, steps: int, dt: float, csv_path: str | None = None):
        self.steps = steps
        self.dt = dt
        self.csv_path = csv_path
        self.frames = self._load_or_generate()

    def _load_or_generate(self):
        if self.csv_path:
            return self._load_csv(self.csv_path)
        return self._generate_synthetic()

    def _load_csv(self, path: str):
        rows = []
        with open(path, "r", newline="") as f:
            reader = csv.DictReader(f)
            for row in reader:
                rows.append(
                    BiometricsFrame(
                        hrv=float(row.get("hrv", 0.5)),
                        eda=float(row.get("eda", 0.5)),
                        emg=float(row.get("emg", 0.2)),
                        resp=float(row.get("resp", 0.5)),
                        imu=float(row.get("imu", 0.2)),
                        temp=float(row.get("temp", 0.5)),
                    )
                )
        if not rows:
            raise ValueError("Biometrics CSV was empty.")
        return rows

    def _generate_synthetic(self):
        frames = []
        for k in range(self.steps):
            t = k * self.dt

            # slow calm/arousal rhythm
            calm_wave = 0.55 + 0.20 * math.sin(0.045 * t)
            arousal_burst = 0.18 * math.sin(0.18 * t + 1.3) + 0.12 * math.sin(0.61 * t)
            focus_burst = 0.22 * max(0.0, math.sin(0.028 * t - 0.7))
            movement = 0.25 + 0.18 * max(0.0, math.sin(0.11 * t + 0.5))
            thermal = 0.52 + 0.08 * math.sin(0.015 * t - 0.8)

            # normalized proxies
            hrv = clamp(calm_wave - 0.30 * max(0.0, arousal_burst))
            eda = clamp(0.35 + 0.65 * max(0.0, arousal_burst))
            emg = clamp(0.18 + 0.55 * focus_burst + 0.25 * movement)
            resp = clamp(0.45 + 0.25 * max(0.0, arousal_burst) - 0.12 * calm_wave)
            imu = clamp(movement + 0.10 * np.random.randn())
            temp = clamp(thermal + 0.03 * np.random.randn())

            frames.append(BiometricsFrame(
                hrv=float(hrv),
                eda=float(eda),
                emg=float(emg),
                resp=float(resp),
                imu=float(imu),
                temp=float(temp),
            ))
        return frames

    def get(self, idx: int) -> BiometricsFrame:
        if idx < len(self.frames):
            return self.frames[idx]
        return self.frames[-1]


class CEF2Engine:
    def __init__(self, n_nodes=14, dt=0.08, seed=7):
        self.n = n_nodes
        self.c = 7
        self.dt = dt
        self.rng = np.random.default_rng(seed)

        # node state
        self.a = 0.20 + 0.08 * self.rng.random((self.n, self.c))
        self.theta = self.rng.uniform(0, 2 * np.pi, size=(self.n, self.c))
        self.m = np.zeros((self.n, self.c))
        self.identity = 0.55 + 0.08 * self.rng.random(self.n)
        self.edge_shell = 0.5 * np.ones(self.n)   # higher = more exploratory
        self.fracture_risk = np.zeros(self.n)

        # intrinsic frequencies and channel templates
        self.omega = np.array([0.42, 0.25, 0.31, 0.48, 0.54, 0.61, 0.33])
        self.decay = np.array([0.22, 0.18, 0.16, 0.24, 0.22, 0.25, 0.28])

        # within-node coupling
        self.K = np.array([
            [0.00, 0.22, 0.21, 0.18, 0.16, 0.10, 0.08],
            [0.22, 0.00, 0.26, 0.18, 0.14, 0.11, 0.08],
            [0.21, 0.26, 0.00, 0.24, 0.19, 0.14, 0.10],
            [0.18, 0.18, 0.24, 0.00, 0.28, 0.20, 0.12],
            [0.16, 0.14, 0.19, 0.28, 0.00, 0.31, 0.14],
            [0.10, 0.11, 0.14, 0.20, 0.31, 0.00, 0.18],
            [0.08, 0.08, 0.10, 0.12, 0.14, 0.18, 0.00],
        ])

        # simple network ring + skip links
        self.G = np.zeros((self.n, self.n))
        for i in range(self.n):
            for j in [(i - 1) % self.n, (i + 1) % self.n, (i + 3) % self.n]:
                self.G[i, j] = 0.08
                self.G[j, i] = 0.08

    def biometric_modulation(self, bio: BiometricsFrame):
        """
        Turn wearable / VR / assistive-device signals into creative-edge controls.
        These are exploratory heuristics, not clinical formulas.
        """
        calm = clamp(0.75 * bio.hrv + 0.15 * (1.0 - bio.resp) + 0.10 * bio.temp)
        arousal = clamp(0.55 * bio.eda + 0.20 * bio.imu + 0.15 * bio.resp + 0.10 * bio.emg)
        effort = clamp(0.65 * bio.emg + 0.20 * bio.imu + 0.15 * (1.0 - bio.hrv))
        grounding = clamp(0.45 * bio.hrv + 0.25 * bio.temp + 0.30 * (1.0 - bio.imu))

        mod = {
            "source_gain": 0.40 + 0.80 * calm,
            "desire_gain": 0.25 + 0.85 * effort,
            "shadow_gain": 0.18 + 0.92 * arousal,
            "recovery_gain": 0.30 + 0.85 * grounding,
            "exploration_gain": 0.10 + 0.90 * clamp(arousal * (1.0 - 0.35 * calm)),
            "memory_gain": 0.22 + 0.70 * clamp(calm + 0.2 * grounding),
            "attention_gain": 0.25 + 0.75 * clamp(0.5 * effort + 0.5 * arousal),
        }
        return mod

    def metrics(self):
        # phase coherence per node
        complex_phase = np.exp(1j * self.theta)
        coh = np.abs(np.mean(complex_phase, axis=1))  # [n]
        amp_var = np.var(self.a, axis=1)
        novelty = clamp(0.55 * np.mean(np.abs(np.gradient(self.a, axis=1)), axis=1) + 0.45 * amp_var * 3.2)
        memory_cont = clamp(np.mean(self.m, axis=1))
        overload = clamp(np.mean(self.a, axis=1) + 0.60 * self.fracture_risk)
        fragmentation = clamp(1.0 - coh + 0.35 * amp_var)
        return coh, novelty, memory_cont, overload, fragmentation

    def creative_edge_score(self):
        coh, novelty, memory_cont, overload, fragmentation = self.metrics()

        # mid-band coherence reward
        coherence_band = 4.0 * coh * (1.0 - coh)

        # source alignment approximated by purple-blue-green agreement
        source_align = clamp(np.mean(self.a[:, :3], axis=1) * coh)

        # desire tension approximated by orange vs blue gap + action readiness
        desire_tension = clamp(np.abs(self.a[:, 4] - self.a[:, 1]) + 0.5 * self.a[:, 5])

        # instability
        risk = clamp(0.6 * fragmentation + 0.4 * overload)

        ec = (
            0.28 * coherence_band
            + 0.20 * novelty
            + 0.18 * source_align
            + 0.17 * desire_tension
            + 0.12 * memory_cont
            - 0.20 * risk
        )
        return clamp(ec), {
            "coherence": coh,
            "novelty": novelty,
            "memory_continuity": memory_cont,
            "overload": overload,
            "fragmentation": fragmentation,
            "source_alignment": source_align,
            "desire_tension": desire_tension,
            "risk": risk,
        }

    def globe_detector(self, edge_scores, coh):
        """
        Marks creative globes where neighboring nodes are jointly in a productive edge band.
        """
        productive = (edge_scores > 0.46) & (coh > 0.32) & (coh < 0.84)
        globes = np.zeros(self.n, dtype=int)
        count = 0
        for i in range(self.n):
            if productive[i] and productive[(i + 1) % self.n]:
                globes[i] = 1
                count += 1
        return globes, count

    def step(self, bio: BiometricsFrame):
        mod = self.biometric_modulation(bio)
        edge_scores, detail = self.creative_edge_score()

        coh = detail["coherence"]
        novelty = detail["novelty"]
        risk = detail["risk"]

        # self-tuning creative-edge shell:
        # too rigid -> increase exploration
        # too chaotic -> increase recovery / reduce exploration
        rigidity = clamp((coh - 0.72) / 0.28)
        under_novel = clamp((0.34 - novelty) / 0.34)
        chaos = clamp((0.25 - coh) / 0.25) + clamp((risk - 0.62) / 0.38)
        explore_drive = 0.55 * rigidity + 0.25 * under_novel + 0.20 * mod["exploration_gain"]
        recover_drive = 0.65 * chaos + 0.20 * mod["recovery_gain"]
        self.edge_shell = clamp(self.edge_shell + self.dt * (explore_drive - recover_drive))

        # channel drives
        source_vec = np.array([1.0, 0.55, 0.62, 0.22, 0.12, 0.08, 0.10]) * mod["source_gain"]
        desire_vec = np.array([0.05, 0.10, 0.18, 0.42, 0.92, 0.68, 0.18]) * mod["desire_gain"]
        shadow_vec = np.array([0.05, 0.08, 0.12, 0.18, 0.28, 0.36, 1.0]) * mod["shadow_gain"]
        memory_vec = np.array([0.10, 1.0, 0.32, 0.12, 0.08, 0.06, 0.10]) * mod["memory_gain"]
        attention_vec = np.array([0.05, 0.12, 0.20, 1.0, 0.38, 0.28, 0.10]) * mod["attention_gain"]

        # per-node exploration noise shaped by biometrics and shell
        noise_scale = 0.02 + 0.07 * mod["exploration_gain"] * self.edge_shell
        phase_noise = self.rng.normal(0.0, noise_scale[:, None] if np.ndim(noise_scale)>0 else noise_scale, size=(self.n, self.c))
        amp_noise = self.rng.normal(0.0, 0.4 * noise_scale[:, None] if np.ndim(noise_scale)>0 else 0.4 * noise_scale, size=(self.n, self.c))

        # convert scalar mod to nodewise scalar
        if np.ndim(noise_scale) == 0:
            noise_scale = np.full(self.n, noise_scale)

        new_a = self.a.copy()
        new_theta = self.theta.copy()

        mean_a = np.mean(self.a, axis=0)

        for p in range(self.n):
            # network field
            net_field = np.sum(self.G[p][:, None] * (self.a - self.a[p]), axis=0)

            # within-node coupling
            phase_diffs = self.theta[p][None, :] - self.theta[p][:, None]
            within = np.sum(self.K * np.sin(phase_diffs), axis=1)

            # stronger identity means more recoverability / less collapse
            identity_guard = 0.55 + 0.65 * self.identity[p]

            drive = (
                source_vec
                + desire_vec
                + 0.55 * memory_vec * self.m[p]
                + 0.35 * attention_vec
                - 0.55 * shadow_vec
                + 0.24 * net_field
                + 0.10 * mean_a
            )

            da = (
                -self.decay * self.a[p]
                + within
                + drive
                + amp_noise[p]
            )

            dtheta = (
                self.omega
                + 0.6 * within
                + phase_noise[p]
            )

            # creative-edge shell shifts between lock and exploration
            lock_pull = (1.0 - self.edge_shell[p]) * 0.12 * (np.mean(self.theta[p]) - self.theta[p])
            explore_push = (0.02 + 0.10 * self.edge_shell[p]) * self.rng.normal(size=self.c)
            dtheta += lock_pull + explore_push

            # fracture / recovery
            self.fracture_risk[p] = clamp(0.86 * self.fracture_risk[p] + 0.18 * risk[p] - 0.12 * mod["recovery_gain"] * self.identity[p])

            # amplitude update
            new_a[p] = np.clip(self.a[p] + self.dt * da / identity_guard, 0.0, 1.4)
            new_theta[p] = (self.theta[p] + self.dt * dtheta) % (2 * np.pi)

        self.a = new_a
        self.theta = new_theta

        # memory update
        self.m = clamp(
            0.94 * self.m
            + self.dt * (
                0.65 * mod["memory_gain"] * self.a
                + 0.25 * self.identity[:, None] * self.a
                - 0.22 * self.fracture_risk[:, None]
            )
        )

        # identity update
        coh, novelty, memory_cont, overload, fragmentation = self.metrics()
        self.identity = clamp(
            self.identity
            + self.dt * (
                0.24 * coh
                - 0.30 * fragmentation
                + 0.18 * memory_cont
                - 0.28 * overload
                + 0.10 * mod["recovery_gain"]
                - 0.08 * self.edge_shell
            )
        )

        edge_scores, detail = self.creative_edge_score()
        globes, globe_count = self.globe_detector(edge_scores, detail["coherence"])

        return {
            "edge_mean": float(np.mean(edge_scores)),
            "edge_max": float(np.max(edge_scores)),
            "coherence_mean": float(np.mean(detail["coherence"])),
            "novelty_mean": float(np.mean(detail["novelty"])),
            "risk_mean": float(np.mean(detail["risk"])),
            "identity_mean": float(np.mean(self.identity)),
            "globe_count": int(globe_count),
            "bio": bio,
            "edge_scores": edge_scores.copy(),
            "coherence": detail["coherence"].copy(),
            "globes": globes.copy(),
        }


def run_simulation(steps=600, nodes=16, dt=0.08, seed=7, biometrics_csv=None, outdir="cef2_output"):
    out = Path(outdir)
    out.mkdir(parents=True, exist_ok=True)

    bio_stream = BiometricsStream(steps=steps, dt=dt, csv_path=biometrics_csv)
    engine = CEF2Engine(n_nodes=nodes, dt=dt, seed=seed)

    hist = {
        "edge_mean": [],
        "edge_max": [],
        "coherence_mean": [],
        "novelty_mean": [],
        "risk_mean": [],
        "identity_mean": [],
        "globe_count": [],
        "hrv": [],
        "eda": [],
        "emg": [],
        "resp": [],
        "imu": [],
        "temp": [],
    }

    snapshots = {}

    for k in range(steps):
        bio = bio_stream.get(k)
        result = engine.step(bio)

        for key in ["edge_mean", "edge_max", "coherence_mean", "novelty_mean", "risk_mean", "identity_mean", "globe_count"]:
            hist[key].append(result[key])

        hist["hrv"].append(bio.hrv)
        hist["eda"].append(bio.eda)
        hist["emg"].append(bio.emg)
        hist["resp"].append(bio.resp)
        hist["imu"].append(bio.imu)
        hist["temp"].append(bio.temp)

        if k in {0, steps // 3, 2 * steps // 3, steps - 1}:
            snapshots[k] = {
                "a": engine.a.copy(),
                "edge_scores": result["edge_scores"].copy(),
                "coherence": result["coherence"].copy(),
                "globes": result["globes"].copy(),
            }

    # save metrics CSV
    metrics_path = out / "cef2_metrics.csv"
    with open(metrics_path, "w", newline="") as f:
        fieldnames = list(hist.keys())
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        for i in range(steps):
            writer.writerow({k: hist[k][i] for k in fieldnames})

    # summary plots
    t = np.arange(steps) * dt
    plt.figure(figsize=(11, 6))
    plt.plot(t, hist["edge_mean"], label="creative edge mean")
    plt.plot(t, hist["coherence_mean"], label="coherence mean")
    plt.plot(t, hist["novelty_mean"], label="novelty mean")
    plt.plot(t, hist["identity_mean"], label="identity mean")
    plt.plot(t, hist["risk_mean"], label="risk mean")
    plt.xlabel("time")
    plt.ylabel("normalized value")
    plt.title("CEF-2 Upsilon Creative Edge Engine")
    plt.legend()
    plt.tight_layout()
    plt.savefig(out / "cef2_summary.png", dpi=180)
    plt.close()

    plt.figure(figsize=(11, 6))
    plt.plot(t, hist["hrv"], label="hrv")
    plt.plot(t, hist["eda"], label="eda")
    plt.plot(t, hist["emg"], label="emg")
    plt.plot(t, hist["resp"], label="resp")
    plt.plot(t, hist["imu"], label="imu")
    plt.plot(t, hist["temp"], label="temp")
    plt.xlabel("time")
    plt.ylabel("input value")
    plt.title("Biometric modulation stream")
    plt.legend()
    plt.tight_layout()
    plt.savefig(out / "cef2_biometrics.png", dpi=180)
    plt.close()

    # final node-channel heatmap
    plt.figure(figsize=(10, 6))
    plt.imshow(engine.a, aspect="auto")
    plt.colorbar(label="amplitude")
    plt.yticks(range(nodes), [f"N{i}" for i in range(nodes)])
    plt.xticks(range(7), [c.split("_")[0] for c in CHANNEL_NAMES], rotation=30)
    plt.title("Final upsilon channel amplitudes")
    plt.tight_layout()
    plt.savefig(out / "cef2_final_heatmap.png", dpi=180)
    plt.close()

    # globe trace
    plt.figure(figsize=(11, 4.5))
    plt.plot(t, hist["globe_count"], label="creative globe count")
    plt.xlabel("time")
    plt.ylabel("count")
    plt.title("Globe births / productive clusters")
    plt.legend()
    plt.tight_layout()
    plt.savefig(out / "cef2_globes.png", dpi=180)
    plt.close()

    summary_text = f"""
CEF-2 run complete.

steps           : {steps}
nodes           : {nodes}
dt              : {dt}
edge_mean_final : {hist['edge_mean'][-1]:.3f}
edge_mean_max   : {max(hist['edge_mean']):.3f}
coherence_final : {hist['coherence_mean'][-1]:.3f}
novelty_final   : {hist['novelty_mean'][-1]:.3f}
identity_final  : {hist['identity_mean'][-1]:.3f}
risk_final      : {hist['risk_mean'][-1]:.3f}
globes_final    : {hist['globe_count'][-1]}
globes_peak     : {max(hist['globe_count'])}
""".strip()

    (out / "README.txt").write_text(summary_text + "\n")

    return {
        "output_dir": str(out),
        "metrics_csv": str(metrics_path),
        "summary_png": str(out / "cef2_summary.png"),
        "biometrics_png": str(out / "cef2_biometrics.png"),
        "heatmap_png": str(out / "cef2_final_heatmap.png"),
        "globes_png": str(out / "cef2_globes.png"),
        "readme": str(out / "README.txt"),
        "summary_text": summary_text,
    }


def build_parser():
    p = argparse.ArgumentParser()
    p.add_argument("--steps", type=int, default=600)
    p.add_argument("--nodes", type=int, default=16)
    p.add_argument("--dt", type=float, default=0.08)
    p.add_argument("--seed", type=int, default=7)
    p.add_argument("--biometrics", type=str, default=None, help="Optional CSV with t,hrv,eda,emg,resp,imu,temp columns.")
    p.add_argument("--outdir", type=str, default="cef2_output")
    return p


if __name__ == "__main__":
    args = build_parser().parse_args()
    result = run_simulation(
        steps=args.steps,
        nodes=args.nodes,
        dt=args.dt,
        seed=args.seed,
        biometrics_csv=args.biometrics,
        outdir=args.outdir,
    )
    print(result["summary_text"])
    print(f"Artifacts saved to: {result['output_dir']}")
