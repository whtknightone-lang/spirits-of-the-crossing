# Sandstone Cave Demo Scene Wiring Guide

## Minimal hierarchy

```text
SandstoneCaveAttunement
  CaveSystems
    DemoBootstrapper
    CaveOriginStoryNarrator    ← NEW: plays founding myth before session
    CaveSessionController
    BreathMovementInterpreter
    PlayerResponseTracker
    PlanetAffinityInterpreter
    PortalUnlockController
    CaveVisualPulseController
    AdaptiveCaveAudioController
    PlanetSigilProjector
    SymbolicOutcomeController
  CenterDisk
    CenterDiskTarget
  PortalRecess
    PortalVisual
    PortalLight
  SculptureRing
    Spirit_Seated
    Spirit_Flow
    Spirit_Dervish
    Spirit_PairA
    Spirit_PairB
  Sigils
    ForestSigil
    WaterSigil
    SkySigil
    MachineSigil
    DarkSigil
```

## Wiring order

0. Add `CaveOriginStoryNarrator` and assign `sessionController`. Set `storyFileName` to `cave_origin_story.json`. The narrator plays the founding myth (Shaman + Chief dialogue) before the session starts. First-time players always see the full story. After 3 sessions it becomes skippable.
1. Add `BreathMovementInterpreter` and leave `useDebugKeyboardInput` on.
2. Add `PlayerResponseTracker` and assign the interpreter.
3. Add `PlanetAffinityInterpreter` and load `Data/planet_profiles_example.json` if you want custom profiles.
4. Add `PortalUnlockController` and assign `PortalVisual` and `PortalLight`.
5. Add `CaveSessionController` and assign:
   - `visualPulseController`
   - `portalUnlockController`
   - `planetAffinityInterpreter`
   - `playerResponseTracker`
6. Add `AdaptiveCaveAudioController` and assign your looping audio stems.
7. Add `PlanetSigilProjector` and bind each planet id to a sigil GameObject and optional accent light.
8. Add `SymbolicOutcomeController` and assign:
   - `sessionController`
   - `planetAffinityInterpreter`
   - `planetSigilProjector`
   - `adaptiveAudioController`
9. For each spirit likeness, add `SpiritLikenessController` and assign:
   - `wallAnchor`
   - `centerDiskTarget`
   - `responseTracker`

## Debug controls

- `1` breath coherence
- `2` flowing movement
- `3` spin stability
- `4` pair sync
- `Q` calm
- `W` joy
- `E` wonder
- `R` distortion
- `T` source alignment

## Expected test flow

1. Start scene.
2. Origin story plays (Shaman and Chief dialogue on the bluff). Wait for it to complete, or press Space to skip if returning player.
3. Hold seated or movement keys to awaken a matching spirit.
3. Sustain a pattern long enough to build sample peaks.
4. Let the 12-minute session complete, or reduce `totalSessionLength` in the inspector for fast tests.
5. On completion, the system evaluates current and achievable planet affinity.
6. `SymbolicOutcomeController` reveals the sigil and pushes audio into the reveal state.
