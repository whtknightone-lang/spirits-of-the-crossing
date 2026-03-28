// SpiritsCrossing — DryadSystem.cs
// The dryads are always there. They were there before the player arrived.
// They will be there after.
//
// HOW DRYADS WORK
//
//   Each dryad is bound to a single ancient tree. Their form exists partially
//   in the bark — visible as grain patterns, shadow shapes, the way the wood
//   seems to shift when you look sideways. As the player's resonance with the
//   forest deepens, the form sharpens. The face becomes clear. The figure becomes
//   present.
//
//   VISIBILITY (per-dryad, updated every 0.5s)
//     Hidden      — you feel watched. Ambient discomfort. Nothing to see.
//     Impression  — faint figure in bark grain. A face you might be imagining.
//     Emerging    — face clearly visible, eyes open, watching you directly.
//     Present     — full form visible. The dryad may nod, raise a hand, or point
//                   toward a nearby stone ring or temple chamber.
//
//   WHISPER SYSTEM
//     When a dryad transitions to Emerging, they deliver their first whisper line.
//     Each subsequent resonance check above the whisper increment delivers the
//     next line. Lines are delivered once and never repeated.
//     The dryad remembers. The player's state is saved to UniverseState.
//
//   GESTURES
//     At Present tier, dryads with canGesture=true will activate a gesture event
//     once per visit. If they are bound to a ruin, the gesture points toward it.
//     Gesture events drive visual animations in the scene (not handled here).
//
//   ALTAR STONE BONUS
//     When the player stands at the altar stone of a nearby ring, this system
//     applies a temporary harmony boost for all dryads in that ring's list.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.World;

namespace SpiritsCrossing.ForestWorld
{
    public class DryadSystem : MonoBehaviour
    {
        public static DryadSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Config")]
        public float scanInterval       = 0.5f;
        public float whisperInterval    = 30f;   // minimum seconds between whisper deliveries
        public float gestureInterval    = 120f;  // minimum seconds between gesture triggers

        [Header("Debug")]
        public bool logVisibilityChanges = false;
        public bool logWhispers          = true;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        // dryadId, new visibility state
        public event Action<string, DryadVisibility> OnDryadVisibilityChanged;
        // dryadId, whisper line text
        public event Action<string, string>          OnDryadWhisper;
        // dryadId, bound ruin id (null if no bound ruin)
        public event Action<string, string>          OnDryadGesture;
        // dryadId, myth fragment key
        public event Action<string, string>          OnDryadPresent;

        // -------------------------------------------------------------------------
        // Internal state
        // -------------------------------------------------------------------------
        private ForestWorldData                  _worldData;
        private float                            _scanTimer;
        private float                            _lastWhisperTime = -999f;
        private float                            _lastGestureTime = -999f;

        // Per-dryad runtime state
        private readonly Dictionary<string, DryadVisibility> _visibility
            = new Dictionary<string, DryadVisibility>();
        private readonly Dictionary<string, int>   _whisperIndex
            = new Dictionary<string, int>();
        private readonly Dictionary<string, bool>  _hasGesturedThisVisit
            = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool>  _hasFiredPresent
            = new Dictionary<string, bool>();

        // Altar bonus: dryadId → bonus harmony (cleared after scan)
        private readonly Dictionary<string, float> _altarBonus
            = new Dictionary<string, float>();

        // Current active forest zone (set by ForestWorldSpawner)
        public string ActiveZoneId { get; private set; }

        // -------------------------------------------------------------------------
        // Initialisation
        // -------------------------------------------------------------------------
        public void Initialise(ForestWorldData data)
        {
            _worldData = data;
            LoadPersistedState();
            Debug.Log($"[DryadSystem] Initialised with {CountAllDryads()} dryads.");
        }

        public void EnterZone(string zoneId)
        {
            ActiveZoneId = zoneId;
            // Reset per-visit gesture flags for dryads in this zone
            var zone = _worldData?.GetZone(zoneId);
            if (zone == null) return;
            foreach (var d in zone.dryads)
                _hasGesturedThisVisit[d.dryadId] = false;

            Debug.Log($"[DryadSystem] Entered zone: {zoneId}");
        }

        public void ExitZone()
        {
            ActiveZoneId = null;
        }

        // -------------------------------------------------------------------------
        // Per-frame scan
        // -------------------------------------------------------------------------
        private void Update()
        {
            _scanTimer += Time.deltaTime;
            if (_scanTimer < scanInterval) return;
            _scanTimer = 0f;

            if (_worldData == null || string.IsNullOrEmpty(ActiveZoneId)) return;
            ScanZone(ActiveZoneId);
        }

        private void ScanZone(string zoneId)
        {
            var zone = _worldData.GetZone(zoneId);
            if (zone == null) return;

            var playerField = VibrationalResonanceSystem.Instance?.PlayerField;
            if (playerField == null) return;

            foreach (var dryad in zone.dryads)
                EvaluateDryad(dryad, playerField);
        }

        private void EvaluateDryad(DryadRecord dryad, VibrationalField playerField)
        {
            // Base harmony against this dryad's affinity field
            float harmony = playerField.WeightedHarmony(dryad.resonanceAffinity);

            // Apply altar bonus if active
            if (_altarBonus.TryGetValue(dryad.dryadId, out float bonus))
                harmony = Mathf.Clamp01(harmony + bonus);

            DryadVisibility newVis = dryad.VisibilityForHarmony(harmony);

            // Check previous visibility
            _visibility.TryGetValue(dryad.dryadId, out DryadVisibility prevVis);

            if (newVis != prevVis)
            {
                _visibility[dryad.dryadId] = newVis;
                OnDryadVisibilityChanged?.Invoke(dryad.dryadId, newVis);

                if (logVisibilityChanges)
                    Debug.Log($"[DryadSystem] {dryad.dryadId} visibility: {prevVis} → {newVis} " +
                              $"(harmony={harmony:F2}, temperament={dryad.temperament})");
            }

            // Whisper delivery
            TryDeliverWhisper(dryad, newVis, harmony);

            // Present event — fires once per dryad lifetime when fully Present
            if (newVis == DryadVisibility.Present)
            {
                if (!_hasFiredPresent.ContainsKey(dryad.dryadId) || !_hasFiredPresent[dryad.dryadId])
                {
                    _hasFiredPresent[dryad.dryadId] = true;
                    if (!string.IsNullOrEmpty(dryad.mythFragment))
                        OnDryadPresent?.Invoke(dryad.dryadId, dryad.mythFragment);
                }

                // Gesture
                TryGesture(dryad);
            }
        }

        // -------------------------------------------------------------------------
        // Whispers
        // -------------------------------------------------------------------------
        private void TryDeliverWhisper(DryadRecord dryad, DryadVisibility vis, float harmony)
        {
            if (vis < DryadVisibility.Emerging) return;
            if (dryad.whisperLines == null || dryad.whisperLines.Count == 0) return;
            if (Time.time - _lastWhisperTime < whisperInterval) return;

            _whisperIndex.TryGetValue(dryad.dryadId, out int idx);
            if (idx >= dryad.whisperLines.Count) return; // all lines delivered

            string line = dryad.whisperLines[idx];
            _whisperIndex[dryad.dryadId] = idx + 1;
            _lastWhisperTime = Time.time;

            OnDryadWhisper?.Invoke(dryad.dryadId, line);
            PersistWhisperIndex(dryad.dryadId, idx + 1);

            if (logWhispers)
                Debug.Log($"[DryadSystem] {dryad.dryadId} whispers [{idx}]: \"{line}\"");
        }

        // -------------------------------------------------------------------------
        // Gestures
        // -------------------------------------------------------------------------
        private void TryGesture(DryadRecord dryad)
        {
            if (!dryad.canGesture) return;
            if (Time.time - _lastGestureTime < gestureInterval) return;

            _hasGesturedThisVisit.TryGetValue(dryad.dryadId, out bool already);
            if (already) return;

            _hasGesturedThisVisit[dryad.dryadId] = true;
            _lastGestureTime = Time.time;

            // boundRuinId is null if dryad points generally toward the forest
            string ruinRef = dryad.bindsToRuin ? dryad.boundRuinId : null;
            OnDryadGesture?.Invoke(dryad.dryadId, ruinRef);

            Debug.Log($"[DryadSystem] {dryad.dryadId} gestures " +
                      $"(toward: {(ruinRef ?? "forest")})");
        }

        // -------------------------------------------------------------------------
        // Altar stone bonus — called by ForestWorldSpawner when player at altar
        // -------------------------------------------------------------------------
        public void ApplyAltarBonus(string ringId)
        {
            var ring = _worldData?.GetRing(ringId);
            if (ring == null) return;

            foreach (var dryadId in ring.nearbyDryadIds)
                _altarBonus[dryadId] = ring.altarResonanceBonus;

            StartCoroutine(ClearAltarBonus(ring.nearbyDryadIds, 10f));
            Debug.Log($"[DryadSystem] Altar bonus applied from ring {ringId} " +
                      $"to {ring.nearbyDryadIds.Count} dryads (+{ring.altarResonanceBonus:F2} for 10s)");
        }

        private IEnumerator ClearAltarBonus(List<string> ids, float delay)
        {
            yield return new WaitForSeconds(delay);
            foreach (var id in ids)
                _altarBonus.Remove(id);
        }

        // -------------------------------------------------------------------------
        // State persistence
        // -------------------------------------------------------------------------
        private void LoadPersistedState()
        {
            // Whisper indices are persisted in UniverseState.dryadWhisperProgress
            // (added as List<DryadWhisperEntry> — see UniverseState extension notes)
            // For now, initialise all to 0 (first-session default).
            if (_worldData == null) return;
            foreach (var zone in _worldData.zones)
                foreach (var d in zone.dryads)
                    if (!_whisperIndex.ContainsKey(d.dryadId))
                        _whisperIndex[d.dryadId] = 0;
        }

        private void PersistWhisperIndex(string dryadId, int newIndex)
        {
            // Hook into UniverseStateManager when DryadWhisperEntry is added.
            // For now: state is held in memory and survives the session.
            _ = newIndex; // suppress unused warning until persistence is wired
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------
        public DryadVisibility GetVisibility(string dryadId)
        {
            _visibility.TryGetValue(dryadId, out DryadVisibility vis);
            return vis;
        }

        public int GetWhisperIndex(string dryadId)
        {
            _whisperIndex.TryGetValue(dryadId, out int idx);
            return idx;
        }

        public bool HasSpokenAll(string dryadId)
        {
            var d = _worldData?.GetDryad(dryadId);
            if (d == null) return false;
            _whisperIndex.TryGetValue(dryadId, out int idx);
            return idx >= d.whisperLines.Count;
        }

        public List<DryadRecord> GetVisibleDryads()
        {
            var result = new List<DryadRecord>();
            if (_worldData == null || string.IsNullOrEmpty(ActiveZoneId)) return result;
            var zone = _worldData.GetZone(ActiveZoneId);
            if (zone == null) return result;
            foreach (var d in zone.dryads)
            {
                _visibility.TryGetValue(d.dryadId, out DryadVisibility vis);
                if (vis > DryadVisibility.Hidden) result.Add(d);
            }
            return result;
        }

        private int CountAllDryads()
        {
            int n = 0;
            if (_worldData == null) return 0;
            foreach (var z in _worldData.zones) n += z.dryads.Count;
            return n;
        }
    }
}
