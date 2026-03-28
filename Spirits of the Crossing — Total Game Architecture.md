# Problem Statement
The project has multiple partial game layers already built as separate prototypes: a cave attunement ritual, portal selection logic, cosmos realm response systems, myth systems, realm-specific gameplay loops, and Python-based resonance/AI simulations. The main architecture task is to unify these into one coherent game structure with clear runtime ownership, shared data contracts, and progression from local ritual scenes to a persistent cosmos.
## Current State
The strongest runtime foundation is the cave attunement flow in `V243_3_SandstoneCaveDemo/Scripts/CaveSessionController.cs`, `V243_3_SandstoneCaveDemo/Scripts/BreathMovementInterpreter.cs`, `V243_3_SandstoneCaveDemo/Scripts/PlanetAffinityInterpreter.cs`, and `V243_3_SandstoneCaveDemo/Scripts/PortalUnlockController.cs`. This already defines the core player-resonance loop: interpret input, accumulate a resonance state, progress through chakra bands, and resolve a planet or portal outcome.
The portal layer in `V243_5_PortalPackage/Assets/Scripts/Portals/ResonancePortalInterpreter.cs` and `V243_5_PortalPackage/Assets/Scripts/Portals/CavePortalTypes.cs` extends that cave flow into a richer choice model with current, achievable, and challenge portals plus reveal/stability values for scene presentation.
The macro-world layer exists in scaffold form in `cosmos_realm_response_pack/Assets/Scripts/Cosmos/CosmosMapDirector.cs`, `cosmos_realm_response_pack/Assets/Scripts/Planets/PlanetHistoryArchive.cs`, and related source-system scripts. This establishes the intended persistent cosmos map, planet memory, and rebirth-cycle loop.
Additional gameplay and simulation layers exist but are still separate: `ElderAirDragon_PlayableLoop/Assets/Scripts` provides a realm-specific playable loop, `Living_Myth_Engine/unity/MythEngine.cs` provides myth-state activation, and the Python prototypes in `rbe_1_runnable_prototype.py`, `rainbow_resonance_processor_v_2-3.py`, and `rue-living-world-V5/run.py` provide research/AI and living-world simulation foundations.
## Proposed Architecture
The game should be organized into six runtime layers plus one offline simulation layer.
### 1. Core Player Resonance Layer
Create a single canonical player-state model used by every scene and subsystem. This layer owns raw input interpretation, derived emotional and movement metrics, session snapshots, and persistent long-term resonance traits. The existing `CavePlayerResonanceState` and `PlayerResponseSample` structures should become shared runtime contracts instead of being cave-specific duplicates.
### 2. Ritual Scene Layer
Treat the Sandstone Cave as the main attunement and calibration ritual. This layer owns chakra progression, spirit awakening, center-disk activation, symbolic outcome presentation, and local portal reveal. `CaveSessionController` remains the main orchestrator for this scene, but its outputs should be formalized as a session result object containing resonance summary, unlocked spirit state, current planet affinity, achievable planet affinity, unlocked portal state, and myth triggers.
### 3. Portal and Realm Selection Layer
Use the V243.5 portal system as the transition layer between ritual and world travel. `ResonancePortalInterpreter` becomes the main scoring service for realm transitions. `PortalUnlockController` and `CavePortalRevealManager` become presentation and commitment components around that service. The portal system should output a normalized realm-travel payload that the outer cosmos can consume directly.
### 4. Persistent Cosmos Layer
The cosmos map becomes the long-term metagame shell. It should own realm availability, planet history, visit counts, aggregate resonance trends, rebirth thresholds, and save/load state. `CosmosMapDirector`, `PlanetNodeController`, `PlanetHistoryArchive`, and `UniverseCycleDirector` should be unified around one persistent game-state container so planets remember the player and change over time.
### 5. Realm Gameplay Layer
Each realm should implement a shared realm interface while remaining mechanically distinct. The Elder Air Dragon loop is the clearest example of a specialized realm controller and should be treated as one concrete realm module. Forest, Ocean, Machine, Dark, and Source realms should follow the same contract: accept the current player resonance profile and planet history, run realm-specific gameplay, emit realm outcomes, and feed those outcomes back to the cosmos and myth systems.
### 6. Myth and World Memory Layer
The myth engine should sit above individual scenes and below the global progression shell. Myths should activate from combinations of ritual outcomes, realm outcomes, companion interactions, and repeated planet-history patterns. Once activated, myths should influence portal bias, companion interpretation, environmental presentation, and future unlock logic. This makes myths a persistent modifier system rather than a local scene mechanic.
### 7. Offline Simulation and AI Layer
Keep the Python resonance engines and living-world prototypes as offline authoring, balancing, and future AI backends. They should not be treated as direct scene dependencies initially. Instead, use them to generate tuning data, NPC behavioral profiles, world-event weights, and simulation-derived content that Unity can consume as assets or serialized config. Direct runtime coupling can be postponed until the Unity loop is stable.
## Shared Data Model
Unify the codebase around a small set of cross-system contracts.
A `PlayerResonanceProfile` should represent persistent identity across sessions and realms.
A `SessionResonanceResult` should represent the result of a single cave ritual.
A `PortalDecision` should represent current, achievable, challenge, and committed portal outcomes.
A `PlanetState` should represent affinity, history, unlock state, and realm-specific memory.
A `RealmOutcome` should represent what happened inside a planet visit and how it changed the player, the myth layer, and the planet.
A `MythState` should represent activated myths and their gameplay modifiers.
A `UniverseState` should aggregate all persistent data for save/load and rebirth-cycle progression.
## Scene Structure
The recommended scene flow is Bootstrap -> Cosmos Map -> Ritual Scene -> Realm Scene -> Cosmos Map, with Universe Rebirth as a special late-game transition. The ritual scene should be reusable as the primary gateway for planet attunement, while realms provide the actual embodied gameplay loops. The cave determines where and how the player enters; the realm determines what the player experiences there; the cosmos records what changed.
## Ownership Boundaries
The ritual layer should never directly mutate long-term cosmos state. It should only emit a session result.
The portal layer should never own persistence. It should only evaluate and commit a travel choice.
The cosmos layer should own all persistence and progression.
Realm scenes should own local gameplay only and emit structured outcomes back to the cosmos.
The myth layer should read from both cosmos and realm outcomes and then publish modifiers back into future sessions.
The Python simulation layer should export authored data rather than drive scene flow directly in the first integration pass.
## Recommended Integration Order
First, extract and centralize the shared resonance and outcome data models so the cave and portal systems stop defining parallel concepts.
Second, define a persistent `UniverseState` and connect the cave session result into the cosmos map and planet-history archive.
Third, convert one realm, preferably the Elder Air Dragon loop, into the standard realm interface and wire its completion back into the global state.
Fourth, connect the myth engine so repeated player behavior and planet visits can activate persistent myth modifiers.
Fifth, add save/load and long-term progression rules around planet history and rebirth thresholds.
Sixth, use the Python prototypes to generate tuning data and AI profiles for spirits, portals, or realm inhabitants.
## Target End State
The final architecture is a resonance-driven game where the player enters a ritual chamber, expresses a measurable internal state, unlocks one or more realm paths, travels into a living planet-specific gameplay loop, changes the world through that experience, accumulates mythic and planetary history, and returns to an evolving cosmos that remembers those choices. The existing cave, portal, cosmos, myth, dragon, and Python systems already cover most of the conceptual stack; the main work now is unification, shared state, and disciplined runtime boundaries.