'use strict';

// ── Utility ───────────────────────────────────────────────────────────────────
function lerp(a, b, t) {
  t = Math.min(1, Math.max(0, t));
  return a + (b - a) * t;
}
function clamp01(x) { return Math.min(1, Math.max(0, x)); }

// ── Enums ─────────────────────────────────────────────────────────────────────
const ChakraBand = Object.freeze({
  Root: 0, Sacral: 1, Solar: 2, Heart: 3, Throat: 4, ThirdEye: 5, Crown: 6
});
const ChakraBandName = ['Root', 'Sacral', 'Solar', 'Heart', 'Throat', 'ThirdEye', 'Crown'];

const SpiritArchetype = Object.freeze({
  Seated: 0, FlowDancer: 1, Dervish: 2, PairA: 3, PairB: 4,
  EarthDragon: 5, FireDragon: 6, WaterDragon: 7, ElderAirDragon: 8
});

// ── CavePlayerResonanceState ──────────────────────────────────────────────────
// Port of V243.SandstoneCave.CavePlayerResonanceState
class CavePlayerResonanceState {
  constructor() {
    this.breathCoherence = 0;
    this.movementFlow    = 0;
    this.spinStability   = 0;
    this.socialSync      = 0;
    this.calm            = 0;
    this.joy             = 0;
    this.wonder          = 0;
    this.distortion      = 0;
    this.sourceAlignment = 0;
  }
  lerpToward(target, t) {
    this.breathCoherence = lerp(this.breathCoherence, target.breathCoherence, t);
    this.movementFlow    = lerp(this.movementFlow,    target.movementFlow,    t);
    this.spinStability   = lerp(this.spinStability,   target.spinStability,   t);
    this.socialSync      = lerp(this.socialSync,      target.socialSync,      t);
    this.calm            = lerp(this.calm,            target.calm,            t);
    this.joy             = lerp(this.joy,             target.joy,             t);
    this.wonder          = lerp(this.wonder,          target.wonder,          t);
    this.distortion      = lerp(this.distortion,      target.distortion,      t);
    this.sourceAlignment = lerp(this.sourceAlignment, target.sourceAlignment, t);
  }
}

// ── ChakraState ───────────────────────────────────────────────────────────────
class ChakraState {
  constructor() {
    this.activeBand = ChakraBand.Root;
    this.progress01 = 0;
    this.isHolding  = false;
    this.holdTimer  = 0;
  }
}

// ── PlayerResponseSample ──────────────────────────────────────────────────────
class PlayerResponseSample {
  constructor() {
    this.stillnessScore       = 0;
    this.flowScore            = 0;
    this.spinScore            = 0;
    this.pairSyncScore        = 0;
    this.calmScore            = 0;
    this.joyScore             = 0;
    this.wonderScore          = 0;
    this.distortionScore      = 0;
    this.sourceAlignmentScore = 0;
  }
}

// ── PlanetVibrationProfile ────────────────────────────────────────────────────
class PlanetVibrationProfile {
  constructor(d) {
    this.planetId         = d.planetId         || 'planet';
    this.groundingWeight  = d.groundingWeight  ?? 1;
    this.flowWeight       = d.flowWeight       ?? 1;
    this.willWeight       = d.willWeight       ?? 1;
    this.heartWeight      = d.heartWeight      ?? 1;
    this.expressionWeight = d.expressionWeight ?? 1;
    this.visionWeight     = d.visionWeight     ?? 1;
    this.crownWeight      = d.crownWeight      ?? 1;
    this.socialWeight     = d.socialWeight     ?? 1;
    this.rotationWeight   = d.rotationWeight   ?? 1;
    this.solitudeWeight   = d.solitudeWeight   ?? 1;
  }
}

// ── BreathMovementInterpreter ─────────────────────────────────────────────────
// Port of V243.SandstoneCave.BreathMovementInterpreter (keyboard debug path)
class BreathMovementInterpreter {
  constructor() {
    this.state            = new CavePlayerResonanceState();
    this.debugSmoothSpeed = 2;
    this._keys            = {};
  }
  onKeyDown(key) { this._keys[key] = true; }
  onKeyUp(key)   { delete this._keys[key]; }

  update(dt) {
    const k      = this._keys;
    const target = new CavePlayerResonanceState();
    target.breathCoherence = k['1'] ? 0.9  : 0.3;
    target.movementFlow    = k['2'] ? 0.85 : 0.2;
    target.spinStability   = k['3'] ? 0.85 : 0.1;
    target.socialSync      = k['4'] ? 0.8  : 0.15;
    target.calm            = k['q'] ? 0.9  : 0.35;
    target.joy             = k['w'] ? 0.85 : 0.25;
    target.wonder          = k['e'] ? 0.85 : 0.3;
    target.distortion      = k['r'] ? 0.7  : 0.15;
    target.sourceAlignment = k['t'] ? 0.9  : 0.25;
    this.state.lerpToward(target, dt * this.debugSmoothSpeed);
  }
}

// ── PlayerResponseTracker ─────────────────────────────────────────────────────
// Port of V243.SandstoneCave.PlayerResponseTracker
class PlayerResponseTracker {
  constructor(interpreter) {
    this.interpreter             = interpreter;
    this.liveState               = new CavePlayerResonanceState();
    this.smoothing               = 2;
    this.peakStillness           = 0;
    this.peakFlow                = 0;
    this.peakSpin                = 0;
    this.peakPairSync            = 0;
    this.averageCalm             = 0;
    this.averageJoy              = 0;
    this.averageWonder           = 0;
    this.averageDistortion       = 0;
    this.averageSourceAlignment  = 0;
    this._sampleTime             = 0;
  }

  update(dt) {
    this.liveState.lerpToward(this.interpreter.state, dt * this.smoothing);
    const s = this.liveState;

    this.peakStillness = Math.max(this.peakStillness, s.breathCoherence * s.calm);
    this.peakFlow      = Math.max(this.peakFlow,      s.movementFlow);
    this.peakSpin      = Math.max(this.peakSpin,      s.spinStability);
    this.peakPairSync  = Math.max(this.peakPairSync,  s.socialSync);

    this._sampleTime += dt;
    const k = clamp01(dt / Math.max(0.0001, this._sampleTime));
    this.averageCalm            = lerp(this.averageCalm,            s.calm,            k);
    this.averageJoy             = lerp(this.averageJoy,             s.joy,             k);
    this.averageWonder          = lerp(this.averageWonder,          s.wonder,          k);
    this.averageDistortion      = lerp(this.averageDistortion,      s.distortion,      k);
    this.averageSourceAlignment = lerp(this.averageSourceAlignment, s.sourceAlignment, k);
  }

  buildSample() {
    const s = new PlayerResponseSample();
    s.stillnessScore       = this.peakStillness;
    s.flowScore            = this.peakFlow;
    s.spinScore            = this.peakSpin;
    s.pairSyncScore        = this.peakPairSync;
    s.calmScore            = this.averageCalm;
    s.joyScore             = this.averageJoy;
    s.wonderScore          = this.averageWonder;
    s.distortionScore      = this.averageDistortion;
    s.sourceAlignmentScore = this.averageSourceAlignment;
    return s;
  }

  resetTracking() {
    this.liveState               = new CavePlayerResonanceState();
    this.peakStillness           = 0;
    this.peakFlow                = 0;
    this.peakSpin                = 0;
    this.peakPairSync            = 0;
    this.averageCalm             = 0;
    this.averageJoy              = 0;
    this.averageWonder           = 0;
    this.averageDistortion       = 0;
    this.averageSourceAlignment  = 0;
    this._sampleTime             = 0;
  }
}

// ── PlanetAffinityInterpreter ─────────────────────────────────────────────────
// Port of V243.SandstoneCave.PlanetAffinityInterpreter
class PlanetAffinityInterpreter {
  constructor() {
    this.planetProfiles           = [];
    this.currentSample            = new PlayerResponseSample();
    this.currentAffinityPlanet    = '';
    this.achievableAffinityPlanet = '';
    this.currentAffinityScore     = 0;
    this.achievableAffinityScore  = 0;
    this.loadDefaultProfiles();
  }

  loadDefaultProfiles() {
    // Matches PlanetAffinityInterpreter.LoadDefaultProfiles()
    this.planetProfiles = [
      new PlanetVibrationProfile({ planetId: 'ForestHeart',  flowWeight: 0.8, heartWeight: 1.4, socialWeight: 1.0, groundingWeight: 0.8 }),
      new PlanetVibrationProfile({ planetId: 'SkySpiral',    rotationWeight: 1.4, visionWeight: 1.1, crownWeight: 1.0, flowWeight: 0.6 }),
      new PlanetVibrationProfile({ planetId: 'SourceVeil',   groundingWeight: 0.7, crownWeight: 1.5, visionWeight: 1.0, solitudeWeight: 1.1 }),
      new PlanetVibrationProfile({ planetId: 'WaterFlow',    flowWeight: 1.5, heartWeight: 0.8, socialWeight: 0.8, groundingWeight: 0.4 }),
      new PlanetVibrationProfile({ planetId: 'MachineOrder', groundingWeight: 1.2, willWeight: 1.0, expressionWeight: 0.6, rotationWeight: 0.2 }),
      new PlanetVibrationProfile({ planetId: 'DarkContrast', visionWeight: 0.7, crownWeight: 0.4, groundingWeight: 0.6, flowWeight: 0.4, socialWeight: 0.2 }),
    ];
  }

  evaluateAffinities() {
    let bestCurrent = -Infinity, bestAchievable = -Infinity;
    this.currentAffinityPlanet    = '';
    this.achievableAffinityPlanet = '';
    const s = this.currentSample;

    for (const p of this.planetProfiles) {
      const current =
        s.stillnessScore       * p.groundingWeight +
        s.flowScore            * p.flowWeight       +
        s.spinScore            * p.rotationWeight   +
        s.pairSyncScore        * p.socialWeight     +
        s.calmScore            * p.groundingWeight  +
        s.joyScore             * p.heartWeight      +
        s.wonderScore          * p.visionWeight     +
        s.sourceAlignmentScore * p.crownWeight      -
        s.distortionScore      * 0.5;

      const achievable = current + s.sourceAlignmentScore * 0.35 + s.wonderScore * 0.2;

      if (current > bestCurrent) {
        bestCurrent = current;
        this.currentAffinityPlanet = p.planetId;
        this.currentAffinityScore  = current;
      }
      if (achievable > bestAchievable) {
        bestAchievable = achievable;
        this.achievableAffinityPlanet = p.planetId;
        this.achievableAffinityScore  = achievable;
      }
    }
  }
}

// ── CaveVisualPulseController ─────────────────────────────────────────────────
// Port of V243.SandstoneCave.CaveVisualPulseController
class CaveVisualPulseController {
  constructor() {
    // RGB arrays matching chakra color fields in CaveVisualPulseController.cs
    this._colors = [
      [255,  51,  38],  // Root      — (1, 0.2, 0.15)
      [255, 115,  26],  // Sacral    — (1, 0.45, 0.1)
      [255, 217,  38],  // Solar     — (1, 0.85, 0.15)
      [ 89, 255, 115],  // Heart     — (0.35, 1, 0.45)
      [ 51, 191, 255],  // Throat    — (0.2, 0.75, 1)
      [115,  89, 255],  // ThirdEye  — (0.45, 0.35, 1)
      [217, 166, 255],  // Crown     — (0.85, 0.65, 1)
    ];
  }
  getChakraColor(band)    { return this._colors[band] || [255, 255, 255]; }
  getPulseIntensity(chakra) {
    return chakra.isHolding ? 1.25 : lerp(0.25, 1.0, chakra.progress01);
  }
}

// ── PortalUnlockController ────────────────────────────────────────────────────
// Port of V243.SandstoneCave.PortalUnlockController
class PortalUnlockController {
  constructor() {
    this.portalUnlocked      = false;
    this.requiredHoldSeconds = 240;
    this.maxDistortion       = 0.25;
    this.requiredCalmOrJoy   = 0.6;
  }

  evaluate(state, chakra) {
    if (this.portalUnlocked) return;
    if (
      chakra.activeBand === ChakraBand.Crown &&
      chakra.isHolding  &&
      chakra.holdTimer  > this.requiredHoldSeconds &&
      state.distortion  < this.maxDistortion &&
      (state.calm > this.requiredCalmOrJoy || state.joy > this.requiredCalmOrJoy)
    ) {
      this.portalUnlocked = true;
    }
  }
}

// ── SpiritLikenessController ──────────────────────────────────────────────────
// Port of V243.SandstoneCave.SpiritLikenessController
class SpiritLikenessController {
  constructor(archetype, wallX, wallY) {
    this.archetype       = archetype;
    this.wallX = wallX;  this.wallY = wallY;
    this.x     = wallX;  this.y     = wallY;
    this.targetX = 0;    this.targetY = 0;
    this.moveSpeed       = 70;   // canvas px/s; scaled by renderer.ringR
    this.awakenThreshold = 0.55;
    this.centerDwellTime = 12;
    this.awakened        = false;
    this.atCenter        = false;
    this.centerTimer     = 0;
  }

  getActivationScore(state) {
    switch (this.archetype) {
      case SpiritArchetype.Seated:
        return state.breathCoherence * 0.5 + state.calm * 0.5;
      case SpiritArchetype.FlowDancer:
        return state.movementFlow * 0.6 + state.joy * 0.4;
      case SpiritArchetype.Dervish:
        return state.spinStability * 0.65 + state.wonder * 0.35;
      case SpiritArchetype.PairA:
      case SpiritArchetype.PairB:
        return state.socialSync * 0.7 + state.joy * 0.3;
      case SpiritArchetype.EarthDragon:
        return state.calm * 0.5 + state.breathCoherence * 0.3 + state.sourceAlignment * 0.2;
      case SpiritArchetype.FireDragon:
        return state.spinStability * 0.45 + state.distortion * 0.35 + state.movementFlow * 0.2;
      case SpiritArchetype.WaterDragon:
        return state.movementFlow * 0.5 + state.socialSync * 0.3 + state.breathCoherence * 0.2;
      case SpiritArchetype.ElderAirDragon:
        return state.wonder * 0.45 + state.sourceAlignment * 0.35 + state.spinStability * 0.2;
      default: return 0;
    }
  }

  update(dt, liveState) {
    const activation = this.getActivationScore(liveState);
    if (!this.awakened && activation >= this.awakenThreshold) {
      this.awakened = true;
    }
    if (!this.awakened) return;

    if (!this.atCenter) {
      this._stepTowards(this.targetX, this.targetY, dt);
      if (Math.hypot(this.x - this.targetX, this.y - this.targetY) < 2) {
        this.x = this.targetX; this.y = this.targetY;
        this.atCenter = true; this.centerTimer = 0;
      }
    } else {
      this.centerTimer += dt;
      if (this.centerTimer >= this.centerDwellTime) {
        this._stepTowards(this.wallX, this.wallY, dt);
        if (Math.hypot(this.x - this.wallX, this.y - this.wallY) < 2) {
          this.x = this.wallX; this.y = this.wallY;
          this.atCenter = false; this.awakened = false; this.centerTimer = 0;
        }
      }
    }
  }

  _stepTowards(tx, ty, dt) {
    const dx = tx - this.x, dy = ty - this.y;
    const dist = Math.hypot(dx, dy);
    if (dist < 1) return;
    const step = Math.min(this.moveSpeed * dt, dist);
    this.x += (dx / dist) * step;
    this.y += (dy / dist) * step;
  }
}

// ── AdaptiveCaveAudioController ───────────────────────────────────────────────
// Port of V243.SandstoneCave.AdaptiveCaveAudioController (state machine only)
class AdaptiveCaveAudioController {
  constructor() {
    this.currentMode  = 'Idle';
    this._forceReveal = false;
  }

  forcePlanetReveal() {
    this._forceReveal = true;
    this.currentMode  = 'PlanetReveal';
  }

  update(liveState, chakra) {
    if (this._forceReveal) return;
    const s = liveState;
    if (chakra.isHolding)                              { this.currentMode = 'CrownHold'; return; }
    if (s.socialSync      >= 0.65)                     { this.currentMode = 'Pair';      return; }
    if (s.spinStability   >= 0.65)                     { this.currentMode = 'Dervish';   return; }
    if (s.movementFlow    >= 0.6)                      { this.currentMode = 'Flow';      return; }
    if (s.breathCoherence >= 0.55 && s.calm >= 0.55)  { this.currentMode = 'Seated';    return; }
    this.currentMode = 'Idle';
  }
}

// ── CaveSessionController ─────────────────────────────────────────────────────
// Port of V243.SandstoneCave.CaveSessionController
class CaveSessionController {
  constructor() {
    this.totalSessionLength = 720;
    this.crownHoldStart     = 420;
    this.chakraState        = new ChakraState();
    this.sessionTimer       = 0;
    this.sessionRunning     = false;
    this.sessionComplete    = false;
    this.onSessionComplete  = null;  // callback()
  }

  startSession() {
    this.sessionTimer    = 0;
    this.sessionRunning  = true;
    this.sessionComplete = false;
    this.chakraState     = new ChakraState();
  }

  update(dt, portalCtrl, affinityInterp, responseTracker) {
    if (!this.sessionRunning || this.sessionComplete) return;

    this.sessionTimer += dt;

    if (this.sessionTimer < this.crownHoldStart) {
      this._updateChakraProgression(this.sessionTimer);
    } else {
      this._enterCrownHold(dt);
    }

    if (portalCtrl && responseTracker) {
      portalCtrl.evaluate(responseTracker.liveState, this.chakraState);
    }

    if (this.sessionTimer >= this.totalSessionLength) {
      this._completeSession(affinityInterp, responseTracker);
    }
  }

  _updateChakraProgression(t) {
    const dur       = this.crownHoldStart / 7;
    const bandIndex = Math.min(6, Math.floor(t / dur));
    this.chakraState.activeBand = bandIndex;
    this.chakraState.progress01 = (t % dur) / dur;
    this.chakraState.isHolding  = false;
    this.chakraState.holdTimer  = 0;
  }

  _enterCrownHold(dt) {
    this.chakraState.activeBand  = ChakraBand.Crown;
    this.chakraState.progress01  = 1;
    this.chakraState.isHolding   = true;
    this.chakraState.holdTimer  += dt;
  }

  _completeSession(affinityInterp, responseTracker) {
    this.sessionComplete = true;
    this.sessionRunning  = false;
    if (responseTracker && affinityInterp) {
      affinityInterp.currentSample = responseTracker.buildSample();
      affinityInterp.evaluateAffinities();
    }
    if (this.onSessionComplete) this.onSessionComplete();
  }
}

// ── SymbolicOutcomeController ─────────────────────────────────────────────────
// Port of V243.SandstoneCave.SymbolicOutcomeController
class SymbolicOutcomeController {
  constructor() {
    this.resultShown      = false;
    this.revealedPlanetId = '';
  }

  update(sessionCtrl, affinityInterp, audioCtrl) {
    if (this.resultShown || !sessionCtrl.sessionComplete) return;
    this._reveal(affinityInterp, audioCtrl);
  }

  _reveal(affinityInterp, audioCtrl) {
    this.resultShown = true;
    let pid = (affinityInterp && affinityInterp.achievableAffinityPlanet) || '';
    if (!pid) pid = (affinityInterp && affinityInterp.currentAffinityPlanet) || '';
    this.revealedPlanetId = pid;
    if (audioCtrl) audioCtrl.forcePlanetReveal();
  }
}

// ── DemoScene ─────────────────────────────────────────────────────────────────
// Mirrors the scene hierarchy in Scene_Wiring_Guide.md
class DemoScene {
  constructor() {
    this.interpreter     = new BreathMovementInterpreter();
    this.responseTracker = new PlayerResponseTracker(this.interpreter);
    this.affinityInterp  = new PlanetAffinityInterpreter();
    this.visualPulse     = new CaveVisualPulseController();
    this.portalCtrl      = new PortalUnlockController();
    this.audioCtrl       = new AdaptiveCaveAudioController();
    this.sessionCtrl     = new CaveSessionController();
    this.symbolicOutcome = new SymbolicOutcomeController();

    // SculptureRing: Spirit_Seated, Spirit_Flow, Spirit_Dervish, Spirit_PairA, Spirit_PairB
    this.spirits = [
      new SpiritLikenessController(SpiritArchetype.Seated,     0, 0),
      new SpiritLikenessController(SpiritArchetype.FlowDancer, 0, 0),
      new SpiritLikenessController(SpiritArchetype.Dervish,    0, 0),
      new SpiritLikenessController(SpiritArchetype.PairA,      0, 0),
      new SpiritLikenessController(SpiritArchetype.PairB,      0, 0),
    ];

    this._affinityTimer = 0;
    this.sessionCtrl.startSession();
  }

  update(dt) {
    this.interpreter.update(dt);
    this.responseTracker.update(dt);
    this.sessionCtrl.update(dt, this.portalCtrl, this.affinityInterp, this.responseTracker);
    this.audioCtrl.update(this.responseTracker.liveState, this.sessionCtrl.chakraState);
    this.symbolicOutcome.update(this.sessionCtrl, this.affinityInterp, this.audioCtrl);

    // Live affinity preview for debug HUD (every ~1 game-second)
    this._affinityTimer += dt;
    if (this._affinityTimer >= 1) {
      this._affinityTimer = 0;
      this.affinityInterp.currentSample = this.responseTracker.buildSample();
      this.affinityInterp.evaluateAffinities();
    }

    for (const spirit of this.spirits) {
      spirit.update(dt, this.responseTracker.liveState);
    }
  }

  onKeyDown(key) { this.interpreter.onKeyDown(key.toLowerCase()); }
  onKeyUp(key)   { this.interpreter.onKeyUp(key.toLowerCase()); }
}
