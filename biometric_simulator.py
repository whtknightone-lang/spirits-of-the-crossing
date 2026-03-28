"""
Biometric Simulator
===================
Generates archetype-specific synthetic biometric time-series (breath, heart
rate, movement) derived from the spirit drive profiles in spirit_profiles.json.

For each archetype it:
  - maps dominant drives to physiological signal parameters
  - generates a realistic 60-second time-series at 20 Hz
  - computes summary statistics used by SimulatedPhysicalInputReader in Unity
  - exports biometric_profiles.json to SpiritsCrossing_Core/BiometricInput/

Signal ↔ Resonance mapping (documented for PhysicalInputBridge.cs)
-------------------------------------------------------------------
breathCoherence  ← regularity of breath cycle period  (low variance = high coherence)
movementFlow     ← smoothness of velocity signal       (low jerk = high flow)
spinStability    ← variance of rotation rate           (low variance = high stability)
calm             ← low HR + low HRV + low movement energy
joy              ← moderate-high HR + smooth movement
wonder           ← sudden pause in movement + breath hold
distortion       ← high jerk + high HR variability + irregular breath
sourceAlignment  ← deep breath coherence + lowest resting HR + stillness
"""

from __future__ import annotations
import json
import math
import pathlib
import numpy as np

PROFILES_PATH = pathlib.Path(__file__).parent / "SpiritsCrossing_Core" / "SpiritAI"  / "spirit_profiles.json"
OUT_PATH      = pathlib.Path(__file__).parent / "SpiritsCrossing_Core" / "BiometricInput" / "biometric_profiles.json"

SAMPLE_RATE = 20   # Hz
DURATION    = 60   # seconds
N           = SAMPLE_RATE * DURATION
T           = np.linspace(0, DURATION, N)
RNG         = np.random.default_rng(seed=77)

# ---------------------------------------------------------------------------
# Archetype physiological parameter table
# Derived from drive weights:
#   rest-dominant  → slow breath, low HR, low movement
#   explore-dominant → fast irregular breath, elevated HR, high jerk
#   signal-dominant  → moderate HR, rhythmic movement, high social sync
#   seek-dominant    → moderate-high HR, smooth directed movement
# ---------------------------------------------------------------------------

ARCHETYPE_PARAMS: dict[str, dict] = {
    "Seated": {
        "breath_rate_hz":      0.20,   # ~12 breaths/min — slow, meditative
        "breath_amplitude":    0.85,   # deep
        "breath_regularity":   0.92,   # very regular
        "hr_bpm_mean":         62.0,
        "hr_bpm_std":          2.5,    # low variability = calm
        "movement_speed_mean": 0.05,   # nearly still
        "movement_jerk_mean":  0.03,
        "rotation_rate_mean":  0.02,
        "breath_hold_prob":    0.08,   # occasional breath holds (wonder/crown)
    },
    "FlowDancer": {
        "breath_rate_hz":      0.28,   # ~17 breaths/min
        "breath_amplitude":    0.72,
        "breath_regularity":   0.78,
        "hr_bpm_mean":         78.0,
        "hr_bpm_std":          5.0,
        "movement_speed_mean": 0.55,
        "movement_jerk_mean":  0.12,   # smooth, low jerk
        "rotation_rate_mean":  0.15,
        "breath_hold_prob":    0.03,
    },
    "Dervish": {
        "breath_rate_hz":      0.42,   # ~25 breaths/min — active spinning
        "breath_amplitude":    0.60,
        "breath_regularity":   0.55,   # less regular
        "hr_bpm_mean":         96.0,
        "hr_bpm_std":          9.0,    # high variability
        "movement_speed_mean": 0.80,
        "movement_jerk_mean":  0.45,   # high jerk — spinning/erratic
        "rotation_rate_mean":  0.75,
        "breath_hold_prob":    0.02,
    },
    "PairA": {
        "breath_rate_hz":      0.25,   # ~15 breaths/min — synchronized
        "breath_amplitude":    0.70,
        "breath_regularity":   0.85,   # high regularity — social entrainment
        "hr_bpm_mean":         72.0,
        "hr_bpm_std":          4.0,
        "movement_speed_mean": 0.40,
        "movement_jerk_mean":  0.10,
        "rotation_rate_mean":  0.20,
        "breath_hold_prob":    0.04,
    },
    "PairB": {
        "breath_rate_hz":      0.30,
        "breath_amplitude":    0.65,
        "breath_regularity":   0.70,   # slightly less regular than PairA
        "hr_bpm_mean":         80.0,
        "hr_bpm_std":          6.5,
        "movement_speed_mean": 0.50,
        "movement_jerk_mean":  0.22,   # more reactive
        "rotation_rate_mean":  0.28,
        "breath_hold_prob":    0.03,
    },
    # ---- Elder Dragon Spirits ----
    "EarthDragon": {
        "breath_rate_hz":      0.15,   # ~9 breaths/min — ancient, vast, slow
        "breath_amplitude":    0.95,   # very deep
        "breath_regularity":   0.96,   # near-perfect regularity
        "hr_bpm_mean":         58.0,   # lowest resting HR
        "hr_bpm_std":          1.8,    # barely varies — rock steady
        "movement_speed_mean": 0.02,   # almost completely still
        "movement_jerk_mean":  0.01,
        "rotation_rate_mean":  0.01,
        "breath_hold_prob":    0.12,   # deep holds — ancient stillness
    },
    "FireDragon": {
        "breath_rate_hz":      0.48,   # ~29 breaths/min — intense, burning
        "breath_amplitude":    0.55,   # shallow, rapid fire
        "breath_regularity":   0.40,   # erratic
        "hr_bpm_mean":        108.0,   # highest HR — fire intensity
        "hr_bpm_std":         12.0,    # very high variability
        "movement_speed_mean": 0.90,   # maximum movement
        "movement_jerk_mean":  0.65,   # explosive jerk
        "rotation_rate_mean":  0.70,   # spinning fire
        "breath_hold_prob":    0.01,
    },
    "WaterDragon": {
        "breath_rate_hz":      0.22,   # ~13 breaths/min — fluid, oceanic
        "breath_amplitude":    0.78,
        "breath_regularity":   0.88,   # very regular — wave-like
        "hr_bpm_mean":         66.0,
        "hr_bpm_std":          3.5,    # gentle variation like tides
        "movement_speed_mean": 0.45,
        "movement_jerk_mean":  0.06,   # ultra-smooth — water flows
        "rotation_rate_mean":  0.18,
        "breath_hold_prob":    0.05,   # underwater breath holds
    },
    "ElderAirDragon": {
        "breath_rate_hz":      0.35,   # ~21 breaths/min — soaring altitude
        "breath_amplitude":    0.70,
        "breath_regularity":   0.75,   # moderate — wind varies
        "hr_bpm_mean":         85.0,   # elevated — in constant flight
        "hr_bpm_std":          7.0,
        "movement_speed_mean": 0.75,   # fast flight
        "movement_jerk_mean":  0.20,   # turbulence
        "rotation_rate_mean":  0.55,   # aerial rolls and spirals
        "breath_hold_prob":    0.04,   # altitude holds
    },
}

# ---------------------------------------------------------------------------
# Signal generators
# ---------------------------------------------------------------------------

def gen_breath(params: dict) -> tuple[np.ndarray, np.ndarray]:
    """Returns (amplitude_signal, is_hold_mask)."""
    f     = params["breath_rate_hz"]
    amp   = params["breath_amplitude"]
    reg   = params["breath_regularity"]
    noise_std = (1.0 - reg) * 0.15

    # Base sinusoidal breath + phase noise
    phase_noise = RNG.normal(0, noise_std, N).cumsum() * 0.3
    signal = amp * np.clip(np.sin(2 * math.pi * f * T + phase_noise), -1, 1)
    amplitude = (signal + 1.0) / 2.0   # 0–1

    # Inject occasional breath holds
    hold_mask = np.zeros(N, dtype=bool)
    if params["breath_hold_prob"] > 0:
        hold_starts = RNG.random(N) < (params["breath_hold_prob"] / SAMPLE_RATE)
        hold_len    = int(SAMPLE_RATE * 3.0)  # ~3 second holds
        for i in np.where(hold_starts)[0]:
            end = min(i + hold_len, N)
            hold_mask[i:end] = True
            amplitude[i:end] = amplitude[i] * 0.95  # freeze near current level

    return amplitude.astype(np.float32), hold_mask


def gen_heart_rate(params: dict) -> np.ndarray:
    """Returns BPM time-series."""
    mean = params["hr_bpm_mean"]
    std  = params["hr_bpm_std"]
    # AR(1) process for realistic HRV
    hr   = np.zeros(N, dtype=np.float32)
    hr[0] = mean
    alpha = 0.97  # memory
    for i in range(1, N):
        hr[i] = alpha * hr[i-1] + (1 - alpha) * mean + RNG.normal(0, std) * 0.3
    return np.clip(hr, 40, 200).astype(np.float32)


def gen_movement(params: dict) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Returns (speed, jerk, rotation_rate) each 0–1."""
    speed_mean = params["movement_speed_mean"]
    jerk_mean  = params["movement_jerk_mean"]
    rot_mean   = params["rotation_rate_mean"]

    speed = np.clip(
        speed_mean + RNG.normal(0, speed_mean * 0.3, N).cumsum() * 0.01,
        0, 1).astype(np.float32)

    jerk  = np.clip(
        jerk_mean  + np.abs(RNG.normal(0, jerk_mean  * 0.5, N)),
        0, 1).astype(np.float32)

    rot   = np.clip(
        rot_mean   + np.abs(RNG.normal(0, rot_mean   * 0.4, N)),
        0, 1).astype(np.float32)

    return speed, jerk, rot


# ---------------------------------------------------------------------------
# Derived resonance metrics from raw signals (mirrors PhysicalInputBridge.cs)
# ---------------------------------------------------------------------------

def compute_breath_coherence(breath_amplitude: np.ndarray, params: dict) -> float:
    """Coherence = 1 - normalised variance of cycle period."""
    # Detect zero-crossings of mid-amplitude
    mid = 0.5
    crossings = np.where(np.diff((breath_amplitude > mid).astype(int)) > 0)[0]
    if len(crossings) < 3:
        return float(params["breath_regularity"])
    periods  = np.diff(crossings) / SAMPLE_RATE
    mean_p   = periods.mean()
    var_norm = periods.std() / max(mean_p, 0.01)
    return float(np.clip(1.0 - var_norm, 0.0, 1.0))


def compute_derived_metrics(breath: np.ndarray, hr: np.ndarray,
                            speed: np.ndarray, jerk: np.ndarray,
                            rotation: np.ndarray, hold_mask: np.ndarray,
                            params: dict) -> dict:
    coherence  = compute_breath_coherence(breath, params)
    hrv        = float(hr.std() / max(hr.mean(), 1.0))
    calm       = float(np.clip((1 - hrv * 8) * (1 - speed.mean() * 0.7) * coherence, 0, 1))
    joy        = float(np.clip((hr.mean() - 55) / 60 * 0.6 + (1 - jerk.mean()) * 0.4, 0, 1))
    distortion = float(np.clip(hrv * 6 + jerk.mean() * 0.5 + (1 - coherence) * 0.3, 0, 1))
    wonder     = float(np.clip(hold_mask.mean() * 8 + (1 - speed.mean()) * 0.3, 0, 1))
    source     = float(np.clip(coherence * 0.5 + calm * 0.3 + wonder * 0.2, 0, 1))
    flow       = float(np.clip((1 - jerk.mean()) * 0.7 + speed.mean() * 0.3, 0, 1))
    spin       = float(np.clip(1 - rotation.std() * 3, 0, 1))

    return {
        "breathCoherence":   round(coherence, 4),
        "movementFlow":      round(flow, 4),
        "spinStability":     round(spin, 4),
        "calm":              round(calm, 4),
        "joy":               round(joy, 4),
        "wonder":            round(wonder, 4),
        "distortion":        round(distortion, 4),
        "sourceAlignment":   round(source, 4),
        "hrMean":            round(float(hr.mean()), 2),
        "hrStd":             round(float(hr.std()), 2),
        "breathRateHz":      round(params["breath_rate_hz"], 4),
        "breathAmplitude":   round(params["breath_amplitude"], 4),
        "movementSpeedMean": round(float(speed.mean()), 4),
        "movementJerkMean":  round(float(jerk.mean()), 4),
        "rotationRateMean":  round(float(rotation.mean()), 4),
    }


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def generate() -> dict:
    profiles = json.loads(PROFILES_PATH.read_text())
    spirit_index = {s["archetypeId"]: s for s in profiles["spirits"]}

    biometric_profiles = {}

    for archetype_id, params in ARCHETYPE_PARAMS.items():
        print(f"  Simulating biometrics: {archetype_id}...")
        breath, hold_mask = gen_breath(params)
        hr                = gen_heart_rate(params)
        speed, jerk, rot  = gen_movement(params)
        derived           = compute_derived_metrics(breath, hr, speed, jerk, rot, hold_mask, params)

        spirit = spirit_index.get(archetype_id, {})

        biometric_profiles[archetype_id] = {
            "archetypeId":        archetype_id,
            "description":        spirit.get("description", ""),
            "signalParameters":   {k: v for k, v in params.items()},
            "derivedResonance":   derived,
            # Per-second envelope for Unity SimulatedPhysicalInputReader
            "breathEnvelope":     [round(float(breath[i * SAMPLE_RATE]), 4)
                                   for i in range(DURATION)],
            "hrEnvelope":         [round(float(hr[i * SAMPLE_RATE]), 2)
                                   for i in range(DURATION)],
            "speedEnvelope":      [round(float(speed[i * SAMPLE_RATE]), 4)
                                   for i in range(DURATION)],
        }

    return biometric_profiles


def main():
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    print("Generating biometric profiles...")
    profiles = generate()
    OUT_PATH.write_text(json.dumps(profiles, indent=2))
    print(f"\nProfiles written to: {OUT_PATH}\n")

    print(f"{'Archetype':<14} {'BreathHz':>9} {'HR':>6} {'Coher':>7} "
          f"{'Calm':>6} {'Joy':>6} {'Dist':>6} {'Source':>7}")
    print("-" * 70)
    for name, p in profiles.items():
        d = p["derivedResonance"]
        print(f"{name:<14} {d['breathRateHz']:>9.3f} {d['hrMean']:>6.1f} "
              f"{d['breathCoherence']:>7.3f} {d['calm']:>6.3f} {d['joy']:>6.3f} "
              f"{d['distortion']:>6.3f} {d['sourceAlignment']:>7.3f}")


if __name__ == "__main__":
    main()
