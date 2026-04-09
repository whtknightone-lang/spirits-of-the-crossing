# Spirits of the Crossing

A resonance-driven game where the player enters a ritual chamber, expresses a measurable internal state, unlocks realm paths, and travels into living planet-specific gameplay loops. The cosmos remembers every choice.

## Project Structure

```
SpiritsCrossing_Core/          Unity C# scripts — the active runtime
  BiometricInput/              Hardware and simulated physical input
  Companions/                  Companion bond, assignment, and behaviour systems
  Cosmos/                      Cosmos map, planet nodes, orbital renderer
  DragonRealms/                Realm-specific gameplay loops (Air, Fire, Water, Earth, Machine, Source)
  ForestWorld / MartialWorld   World-specific spawners and types
  OceanWorld / SkyWorld /
  FireWorld / MachineWorld /
  SourceWorld/                 Source world spawner, zone logic, UpsilonSourceLayer
  Lifecycle/                   Born → InSource → Rebirth cycle system
  Memory/                      Resonance learning state and memory system
  Runtime/                     Universe state, portal, myth, ruin, bootstrap
  SharedContracts/             Cross-system data contracts (PlayerResonanceState, etc.)
  SpiritAI/                    Spirit brain orchestrator, controllers, drive profiles
  Vibration/                   Vibrational resonance system, Upsilon node brains, river
  VR/                          VR input adapter, haptics, bootstrap installer
  World/                       World system, terrain resonance
  UpsilonPath/                 UpsilonPath sphere layer (see below)
  Tests/                       Play Mode unit tests
```

## Architecture

The game runs six runtime layers on top of shared data contracts:

1. **Core Player Resonance** — `BreathMovementInterpreter` → `SpiritBrainOrchestrator` → `PlayerResonanceState`
2. **Ritual Scene** — Sandstone Cave attunement, chakra progression, spirit awakening, portal reveal
3. **Portal & Realm Selection** — `ResonancePortalInterpreter` scores and commits realm travel
4. **Persistent Cosmos** — `CosmosMapDirector`, `PlanetHistoryArchive`, `UniverseStateManager`
5. **Realm Gameplay** — Each realm implements `IRealmController`, emits `RealmOutcome`
6. **Myth & World Memory** — `MythInterpreter` activates persistent modifiers from repeated patterns

See `Spirits of the Crossing — Total Game Architecture.md` for the full specification.

---

## UpsilonPath Integration

**Merged:** April 2026

The UpsilonPath sphere layer is now part of `SpiritsCrossing_Core`. It provides the deeper nervous system beneath the game: a 7-node frequency-band sphere model that maps player perception, tuning state, and resonance memory onto a continuous signal that the Source World and spirit AI systems consume.

### What was integrated

#### `SpiritsCrossing_Core/UpsilonPath/` — new folder

| File | Role |
|---|---|
| `UpsilonNode.cs` | `UpsilonQuestionNode` enum (7 values), `UpsilonFrequencyBand`, `UpsilonNodeState`, `UpsilonNodeFactory` |
| `UpsilonSphere.cs` | Main sphere `MonoBehaviour` — 7-node oscillator, computes `overallCoherence`, `sourceReception`, `perceptionReadiness`, `resonancePressure` |
| `UpsilonResonanceMemory.cs` | Ring-buffer of `ResonanceMemoryTrace` entries captured when coherence exceeds threshold |
| `UpsilonPerceptionBridge.cs` | Derives `clarity`, `distortion`, `dominantNode`, `stationShiftReadiness` from sphere output |
| `UpsilonSphereBootstrap.cs` | Self-assembles all four components on `Awake`. Drop on any `GameObject` to activate the sphere stack |

Namespace: `UpsilonPath.Sphere` (preserved as-is from the original UpsilonPath project).

#### `SpiritsCrossing_Core/SourceWorld/UpsilonSourceLayer.cs` — moved into Core

A 7-band Kuramoto-coupled layer that converts player/world resonance into a continuous `UpsilonSourceSignal` (`sourceDrive`, `aiDrive`, `unifiedIntent`, `coherence`, `invitation`, `emergence`, `stillness`). Emits transient boosts into `VibrationalResonanceSystem` each tick. Designed to sit above the existing myth, pool, gate, and well systems without replacing them.

#### `SpiritsCrossing_Core/SourceWorld/SourceWorldSpawner.cs` — three methods added

`UpsilonSourceLayer` calls these to drive its band targets:

- `ZoneCompletion01()` — ratio of discovered features to total features in the active zone
- `AverageGateProgress01()` — mean gate progress (in-progress and completed) across threshold gates
- `SourceDiscoveryBias()` — weighted toward deep source discoveries (inner chambers, veil pools, origin wells)

#### `SpiritsCrossing_Core/SpiritAI/SpiritBrainOrchestrator.cs` — optional sphere hookup

An optional `upsilonBridge` inspector field was added. When assigned, the sphere's `clarity` blends into `sourceAlignment` (30% weight) and `distortion` blends into `distortion` (30% weight) in the shared `PlayerResonanceState`. The cave interpreter remains the primary signal source.

### Design decisions

**Namespace preserved.** `UpsilonPath.Sphere` is kept as-is. It coexists cleanly with `SpiritsCrossing.*` namespaces — the two `UpsilonNodeState` types in different namespaces have no conflict.

**`UpsilonSphere.Awake()` is intentionally empty at runtime.** The bootstrap exclusively owns node initialization. `Reset()` still auto-initializes for editor convenience. This makes `initializeDefaultNodes = false` meaningful for manual control scenarios.

**Additive, not replacing.** Nothing in the existing Vibration layer (`UpsilonNodeBrain`, `UpsilonSphereAI`, `VibrationalResonanceSystem`) was changed. The sphere layer adds a player-facing perception model on top of the existing creature/AI resonance physics.

### Scene setup

1. Add `UpsilonSphereBootstrap` to any persistent `GameObject` (e.g. alongside `SpiritBrainOrchestrator`). It self-assembles `UpsilonSphere`, `UpsilonResonanceMemory`, and `UpsilonPerceptionBridge`.
2. Drag the resulting `UpsilonPerceptionBridge` component into the `upsilonBridge` slot on `SpiritBrainOrchestrator` to activate the sphere → player state blend.
3. Add `UpsilonSourceLayer` to the `SourceWorldSpawner` `GameObject`. Call `EnterSourceZone(zone, spawner)` on entry and `TickLayer(dt, spawner)` each frame.

### Tests

Play Mode tests are in `SpiritsCrossing_Core/Tests/UpsilonSphereBootstrapTests.cs`. Run via **Window → General → Test Runner → PlayMode**.

10 tests cover: component self-assembly, reference wiring, node count and uniqueness, default value validity, guard flags (`autoCreate`, `initNodes`), idempotency, perception target forwarding, and live coherence output.

---

## Assembly Definitions

`SpiritsCrossing_Core/SpiritsCrossing.Core.asmdef` defines the main runtime assembly. The test assembly (`SpiritsCrossing.Tests.UpsilonPath.asmdef`) references it explicitly.

If the project already uses a different assembly structure, update the `references` field in `SpiritsCrossing_Core/Tests/SpiritsCrossing.Tests.UpsilonPath.asmdef` to match.
