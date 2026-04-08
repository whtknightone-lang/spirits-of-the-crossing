# Upsilon Unity Starter Bridge + In-Game Skin System

This bundle gives you a first working bridge between a Python CEF-2 runtime and a Unity scene.

## What is included

- **Python UDP sender** that emits CEF-2 style state packets at a steady rate.
- **Unity UDP receiver** that reads packets into a live state object.
- **Skin controller** that can drive a shared "skin" layer for:
  - player avatar
  - AI agents
  - animals/companions
- **OpenXR haptic hook** for controller vibration based on fracture/desire/edge intensity.
- **Scriptable skin profiles** so each species or character class can react differently while sharing the same upsilon logic.

## High-level architecture

Sensors / AI state -> Python CEF-2 runtime -> UDP JSON packets -> Unity receiver ->
Upsilon state bus -> visuals / haptics / AI / animals / player skin

## Unity setup

1. Create a Unity 2022 LTS+ or Unity 6 project.
2. Import these scripts into `Assets/Scripts/CEF/`.
3. Enable **Input System** and **OpenXR** in Project Settings.
4. Create an empty GameObject called `CEFBridge` and attach:
   - `UpsilonUdpReceiver`
   - optionally `UpsilonHapticsDriver`
5. Create materials for skin glow and assign them to renderers.
6. On player / AI / animal prefabs attach:
   - `UpsilonSkinController`
   - `UpsilonEntityStateRouter`
7. Create one or more `UpsilonSkinProfile` assets and assign them per entity.

## Python test setup

Run:

```bash
python3 cef2_udp_bridge.py
```

By default it sends packets to `127.0.0.1:7777` at 30 Hz.

## Packet format

```json
{
  "t": 12.48,
  "entity": "player",
  "creative_edge": 0.73,
  "coherence": 0.58,
  "novelty": 0.66,
  "identity": 0.71,
  "fracture_risk": 0.19,
  "source_alignment": 0.62,
  "desire_tension": 0.54,
  "shadow_pressure": 0.21,
  "channels": [0.41, 0.55, 0.62, 0.77, 0.69, 0.48, 0.33],
  "globe_events": [{"type":"invention","strength":0.82}],
  "biometrics": {
    "hrv": 0.64,
    "eda": 0.32,
    "emg": 0.58,
    "resp": 0.61,
    "imu": 0.45,
    "temp": 0.51
  }
}
```

## Skin model

The in-game skin is not just a shader. It is a reactive body layer that maps CEF-2 state into:

- emission
- pulse speed
- shell distortion
- gait / idle variance hooks
- ear/tail/wing/body secondary motion hooks for animals
- controller haptics for player embodiment
- color shift across seven upsilon channels

### Suggested profile differences

- **Player**: strongest haptics, clearest HUD, higher identity protection.
- **AI agents**: more visible source/desire conflict, stronger globe participation.
- **Animals**: emphasize motion rhythm, body warmth, field sensitivity, herd resonance.

## Recommended next step

Use this bridge first with a single room and three prefabs:

- player orb/humanoid
- one AI spirit
- one animal companion

Once packets are moving, add biometrics and world reactions.
