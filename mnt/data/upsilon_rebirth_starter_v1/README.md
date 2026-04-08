# Upsilon Rebirth Starter v1

This starter is meant to sit **around** your existing `UpsilonSphereAI.cs`.
It does not replace that class. It gives you a small scene bootstrap you can edit into a larger game loop.

## Included scripts
- `SourceBrain.cs` — global Source field + registries
- `WorldFieldEmitter.cs` — resonance zones like Grove / Flame / Shrine
- `RebirthChamber.cs` — catches agents during Respawning and releases them on Born
- `EmbodiedAgentController.cs` — body wrapper for movement + emitter sensing
- `SimulationBootstrap.cs` — one-click scene bootstrap

## Expected existing dependencies
These scripts assume your project already contains:
- `UpsilonSphereAI`
- `VibrationalField`
- `RiverSpherePhase`
- `SpiritDriveMode`

## Fast bootstrap in Unity
1. Put these scripts in your Unity project, ideally next to `UpsilonSphereAI.cs`.
2. Open an empty test scene.
3. Create one empty GameObject called `Bootstrap`.
4. Add `SimulationBootstrap` to it.
5. Press Play.

The bootstrapper will auto-create:
- `SourceBrain`
- `RebirthChamber`
- 3 field emitters
- 12 agents

## Best next edits
- Increase `agentCount` to 50, then 100.
- Swap the primitive spheres for your spirit prefab.
- Add relation memory to `UpsilonSphereAI` or a companion component.
- Replace simple movement with NavMesh or ECS.
- Add a debug HUD showing phase, life count, dominant drive, and memory band.

## Scaling notes
For bigger populations:
- Keep `UpsilonSphereAI` as the identity / memory core.
- Move movement + sensing into ECS or jobs later.
- Convert `WorldFieldEmitter` into cached field samples or grid cells.
- Let `SourceBrain` become the rebirth scheduler and world-memory layer.
