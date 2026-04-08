#!/usr/bin/env python3
"""Simple CEF-2 UDP sender for Unity starter bridge.

Sends synthetic but structured CEF-2 packets for three entity types:
player, ai_1, animal_1.
"""
from __future__ import annotations

import json
import math
import random
import socket
import time
from dataclasses import dataclass
from typing import Dict, List

HOST = "127.0.0.1"
PORT = 7777
HZ = 30.0


@dataclass
class EntityState:
    name: str
    phase: float
    edge_bias: float
    coherence_bias: float
    novelty_bias: float


def clamp01(x: float) -> float:
    return max(0.0, min(1.0, x))


def make_channels(t: float, phase: float) -> List[float]:
    vals = []
    for i in range(7):
        v = 0.5 + 0.25 * math.sin(t * (0.7 + i * 0.11) + phase + i * 0.5)
        v += 0.08 * math.sin(t * (1.7 + i * 0.07) - phase * 0.3)
        vals.append(clamp01(v))
    return vals


def biometrics_from_state(coherence: float, novelty: float, desire: float, fracture: float, t: float) -> Dict[str, float]:
    hrv = clamp01(0.7 * coherence + 0.15 * math.sin(t * 0.3) - 0.3 * fracture)
    eda = clamp01(0.25 + 0.55 * desire + 0.2 * fracture)
    emg = clamp01(0.2 + 0.4 * novelty + 0.25 * abs(math.sin(t * 1.7)))
    resp = clamp01(0.45 + 0.2 * math.sin(t * 0.2) + 0.2 * coherence)
    imu = clamp01(0.35 + 0.35 * novelty + 0.2 * abs(math.sin(t * 1.2)))
    temp = clamp01(0.48 + 0.08 * math.sin(t * 0.08) + 0.06 * coherence)
    return {
        "hrv": hrv,
        "eda": eda,
        "emg": emg,
        "resp": resp,
        "imu": imu,
        "temp": temp,
    }


def build_packet(entity: EntityState, t: float) -> Dict[str, object]:
    coherence = clamp01(0.55 + entity.coherence_bias + 0.18 * math.sin(t * 0.41 + entity.phase))
    novelty = clamp01(0.52 + entity.novelty_bias + 0.22 * math.sin(t * 0.73 + entity.phase * 1.7))
    identity = clamp01(0.65 + 0.12 * math.sin(t * 0.2 + entity.phase * 0.4) - 0.15 * max(0.0, novelty - coherence))
    source_alignment = clamp01(0.58 + 0.16 * math.sin(t * 0.19 + entity.phase * 0.3))
    desire_tension = clamp01(0.46 + 0.22 * math.sin(t * 0.57 - entity.phase * 0.6))
    shadow_pressure = clamp01(0.18 + 0.15 * max(0.0, novelty - coherence) + 0.08 * abs(math.sin(t * 1.1 + entity.phase)))
    fracture_risk = clamp01(0.12 + 0.5 * max(0.0, novelty - coherence) + 0.35 * max(0.0, 0.4 - identity))
    creative_edge = clamp01(
        entity.edge_bias + 1.1 * (4.0 * coherence * (1.0 - coherence)) * 0.35 + novelty * 0.22 + source_alignment * 0.18 + desire_tension * 0.15 - fracture_risk * 0.18
    )

    channels = make_channels(t, entity.phase)

    globe_events = []
    if creative_edge > 0.72 and random.random() < 0.08:
        globe_events.append({"type": "invention", "strength": round(creative_edge, 3)})
    if source_alignment > 0.72 and random.random() < 0.05:
        globe_events.append({"type": "source", "strength": round(source_alignment, 3)})

    return {
        "t": round(t, 4),
        "entity": entity.name,
        "creative_edge": round(creative_edge, 4),
        "coherence": round(coherence, 4),
        "novelty": round(novelty, 4),
        "identity": round(identity, 4),
        "fracture_risk": round(fracture_risk, 4),
        "source_alignment": round(source_alignment, 4),
        "desire_tension": round(desire_tension, 4),
        "shadow_pressure": round(shadow_pressure, 4),
        "channels": [round(x, 4) for x in channels],
        "globe_events": globe_events,
        "biometrics": biometrics_from_state(coherence, novelty, desire_tension, fracture_risk, t),
    }


def main() -> None:
    entities = [
        EntityState("player", phase=0.0, edge_bias=0.18, coherence_bias=0.03, novelty_bias=0.00),
        EntityState("ai_1", phase=0.8, edge_bias=0.14, coherence_bias=-0.02, novelty_bias=0.08),
        EntityState("animal_1", phase=1.7, edge_bias=0.16, coherence_bias=0.05, novelty_bias=-0.03),
    ]
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    dt = 1.0 / HZ
    start = time.perf_counter()
    print(f"Sending CEF-2 packets to udp://{HOST}:{PORT} at {HZ:.1f} Hz")
    try:
        while True:
            now = time.perf_counter() - start
            for entity in entities:
                payload = json.dumps(build_packet(entity, now), separators=(",", ":")).encode("utf-8")
                sock.sendto(payload, (HOST, PORT))
            time.sleep(dt)
    except KeyboardInterrupt:
        print("Stopped.")


if __name__ == "__main__":
    main()
