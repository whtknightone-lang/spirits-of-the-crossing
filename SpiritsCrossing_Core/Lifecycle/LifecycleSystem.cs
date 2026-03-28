// SpiritsCrossing — LifecycleSystem.cs
// Manages the player's cosmological Birth / Source Drop-In / Rebirth cycle.
//
// Cycle summary:
//   Born       — Living in the world. sourceConnectionLevel accumulates.
//   InSource   — Released. Elder Dragon communion. Cosmos observation.
//                Vibrational field purifies (distortion fades, Source rises).
//   Rebirth    — Gifts applied. Ancient memory imprinted. New Born begins.
//
// The cycle is NOT loss — it is depth. Each rebirth carries forward:
//   • sourceConnectionLevel boost
//   • Element may shift toward the communed dragon
//   • One companion permanently bonded deeper
//   • A myth permanently reinforced
//   • An ancient memory field that colors every future session

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Memory;
using SpiritsCrossing.Vibration;
using SpiritsCrossing.Companions;

namespace SpiritsCrossing.Lifecycle
{
    public class LifecycleSystem : MonoBehaviour
    {
        public static LifecycleSystem Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<PlayerCyclePhase> OnPhaseChanged;
        public event Action                   OnSourceDropIn;
        public event Action                   OnRebirthBegins;
        public event Action<List<string>>     OnRebirthComplete; // list of gift keys

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        public PlayerCyclePhase CurrentPhase =>
            UniverseStateManager.Instance?.Current.lifecycle?.currentPhase ?? PlayerCyclePhase.Born;

        public bool IsInSource => CurrentPhase == PlayerCyclePhase.InSource;
        public int  CycleCount => UniverseStateManager.Instance?.Current.lifecycle?.cycleCount ?? 0;

        // -------------------------------------------------------------------------
        // Drop-In — player chooses to enter the Source
        // -------------------------------------------------------------------------
        public bool CanDropIn()
        {
            var scl = ResonanceMemorySystem.Instance?.SourceConnectionLevel ?? 0f;
            var lc  = UniverseStateManager.Instance?.Current.lifecycle;
            return lc?.CanDropIn(scl) ?? false;
        }

        public void InitiateSourceDropIn()
        {
            if (!CanDropIn())
            {
                Debug.Log("[LifecycleSystem] Cannot drop in — sourceConnectionLevel too low (need 0.40+).");
                return;
            }

            var universe = UniverseStateManager.Instance?.Current;
            if (universe == null) return;

            var lc  = universe.lifecycle;
            var scl = ResonanceMemorySystem.Instance?.SourceConnectionLevel ?? 0f;

            lc.currentPhase      = PlayerCyclePhase.InSource;
            lc.sourceDropInUtc   = DateTime.UtcNow.ToString("o");
            lc.dropInSourceLevel = scl;
            lc.communedDragonElement  = null;
            lc.communionDepth         = 0f;
            lc.rebirthGiftKeys.Clear();

            // Purify vibrational field — distortion falls, violet/indigo rise
            // Applied to the persistent resonance snapshot so future sessions feel the shift
            universe.persistentResonance.distortionScore     *= 0.2f;
            universe.persistentResonance.sourceAlignmentScore =
                Mathf.Min(1f, universe.persistentResonance.sourceAlignmentScore + 0.15f);
            universe.persistentResonance.wonderScore =
                Mathf.Min(1f, universe.persistentResonance.wonderScore + 0.10f);

            UniverseStateManager.Instance.Save();
            OnPhaseChanged?.Invoke(PlayerCyclePhase.InSource);
            OnSourceDropIn?.Invoke();

            Debug.Log($"[LifecycleSystem] Source Drop-In. Cycle={lc.cycleCount} SCL={scl:F3}");
        }

        // -------------------------------------------------------------------------
        // Rebirth — player chooses to return from the Source
        // Called by SourceCommunionSystem when the player decides to leave.
        // -------------------------------------------------------------------------
        public void InitiateRebirth(string communedDragonElement, float communionDepth,
                                     string deepenedCompanionId, VibrationalField dragonField)
        {
            if (!IsInSource)
            {
                Debug.LogWarning("[LifecycleSystem] InitiateRebirth called outside InSource phase.");
                return;
            }

            OnRebirthBegins?.Invoke();

            var universe = UniverseStateManager.Instance?.Current;
            var lc = universe?.lifecycle;
            if (lc == null) return;

            lc.communedDragonElement    = communedDragonElement;
            lc.communionDepth           = communionDepth;
            lc.deepenedCompanionAnimalId = deepenedCompanionId;
            lc.currentPhase             = PlayerCyclePhase.Rebirth;
            lc.rebirthUtc               = DateTime.UtcNow.ToString("o");

            var gifts = SelectGifts(lc, communionDepth);
            lc.rebirthGiftKeys.Clear();
            foreach (var g in gifts) lc.rebirthGiftKeys.Add(g.ToString());

            ApplyGifts(universe, lc, gifts, communedDragonElement, deepenedCompanionId, dragonField);

            lc.cycleCount++;
            lc.currentPhase = PlayerCyclePhase.Born;

            UniverseStateManager.Instance.Save();
            OnPhaseChanged?.Invoke(PlayerCyclePhase.Born);
            OnRebirthComplete?.Invoke(lc.rebirthGiftKeys);

            Debug.Log($"[LifecycleSystem] Rebirth complete. Cycle={lc.cycleCount} " +
                      $"Dragon={communedDragonElement} Depth={communionDepth:F2} " +
                      $"Gifts={string.Join(", ", lc.rebirthGiftKeys)}");
        }

        // -------------------------------------------------------------------------
        // Gift selection — based on communion depth
        // -------------------------------------------------------------------------
        private List<RebirthGift> SelectGifts(LifecycleState lc, float depth)
        {
            var gifts = new List<RebirthGift> { RebirthGift.SourceBoost };  // always

            if (depth >= 0.40f) gifts.Add(RebirthGift.CompanionDeepening);
            if (depth >= 0.55f) gifts.Add(RebirthGift.MythAwakening);
            if (depth >= 0.70f) gifts.Add(RebirthGift.ElementShift);
            if (depth >= 0.85f || lc.cycleCount >= 2)
                                 gifts.Add(RebirthGift.AncientMemory);
            return gifts;
        }

        // -------------------------------------------------------------------------
        // Apply gifts to UniverseState
        // -------------------------------------------------------------------------
        private void ApplyGifts(UniverseState universe, LifecycleState lc,
                                 List<RebirthGift> gifts, string dragonElement,
                                 string deepenedCompanionId, VibrationalField dragonField)
        {
            var learning = universe.learningState;
            var myth     = universe.mythState;

            foreach (var gift in gifts)
            {
                switch (gift)
                {
                    case RebirthGift.SourceBoost:
                        learning.sourceConnectionLevel =
                            Mathf.Clamp01(learning.sourceConnectionLevel + 0.10f);
                        Debug.Log($"[LifecycleSystem] Gift: SourceBoost → scl={learning.sourceConnectionLevel:F3}");
                        break;

                    case RebirthGift.ElementShift:
                        if (!string.IsNullOrEmpty(dragonElement))
                        {
                            string prev = learning.dominantElement;
                            learning.dominantElement = dragonElement;
                            Debug.Log($"[LifecycleSystem] Gift: ElementShift {prev} → {dragonElement}");
                        }
                        break;

                    case RebirthGift.CompanionDeepening:
                        if (!string.IsNullOrEmpty(deepenedCompanionId))
                        {
                            var bond = universe.GetOrCreateBond(deepenedCompanionId);
                            bond.bondLevel = Mathf.Clamp01(bond.bondLevel + 0.20f);
                            Debug.Log($"[LifecycleSystem] Gift: CompanionDeepening {deepenedCompanionId} → {bond.bondLevel:F2}");
                        }
                        break;

                    case RebirthGift.MythAwakening:
                        string mythKey = ElementToMythKey(dragonElement);
                        myth.Activate(mythKey, "rebirth", 0.80f);
                        myth.Activate("elder",            "rebirth", 0.60f);
                        Debug.Log($"[LifecycleSystem] Gift: MythAwakening {mythKey} + elder");
                        break;

                    case RebirthGift.AncientMemory:
                        if (dragonField != null)
                        {
                            lc.ancientMemoryField = dragonField;
                            lc.ancientMemoryStrength = Mathf.Clamp01(
                                0.20f + lc.cycleCount * 0.10f);
                            Debug.Log($"[LifecycleSystem] Gift: AncientMemory strength={lc.ancientMemoryStrength:F2}");
                        }
                        break;
                }
            }

            // Ancient memory blends into persistent resonance permanently
            if (lc.ancientMemoryField != null && lc.ancientMemoryStrength > 0f)
            {
                float alpha = lc.MemoryBlendAlpha();
                ApplyMemoryToResonance(universe, lc.ancientMemoryField, alpha);
            }
        }

        private void ApplyMemoryToResonance(UniverseState u, VibrationalField memory, float alpha)
        {
            // Ancient memory lifts the player's persistent resonance toward the dragon's field
            u.persistentResonance.calmScore = Mathf.Lerp(
                u.persistentResonance.calmScore, memory.green, alpha);
            u.persistentResonance.wonderScore = Mathf.Lerp(
                u.persistentResonance.wonderScore, memory.violet, alpha);
            u.persistentResonance.sourceAlignmentScore = Mathf.Lerp(
                u.persistentResonance.sourceAlignmentScore, memory.SourceAlignment(), alpha);
        }

        private static string ElementToMythKey(string element) => element switch
        {
            "Fire"   => "fire",
            "Earth"  => "forest",
            "Water"  => "ocean",
            "Air"    => "sky",
            "Source" => "source",
            _        => "elder"
        };

        // -------------------------------------------------------------------------
        // Public queries
        // -------------------------------------------------------------------------
        public LifecycleState GetLifecycleState() =>
            UniverseStateManager.Instance?.Current.lifecycle;

        public string GetReturnGreeting()
        {
            var lc = GetLifecycleState();
            if (lc == null || lc.cycleCount == 0) return "The cycle begins.";
            return lc.cycleCount switch
            {
                1 => $"You carry the {lc.communedDragonElement} dragon's memory.",
                2 => $"The {lc.communedDragonElement} path deepens. Ancient echoes wake.",
                _ => $"Cycle {lc.cycleCount}. The Source remembers every return."
            };
        }
    }
}
