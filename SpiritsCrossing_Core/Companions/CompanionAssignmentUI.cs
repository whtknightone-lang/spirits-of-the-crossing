// SpiritsCrossing — CompanionAssignmentUI.cs
// Framework-agnostic data provider for the companion assignment and management UI.
// Works equally for Unity UGUI, VR world-space panels, or flat debug readouts.
// Does NOT render anything — returns structured data that any UI system consumes.
//
// PLAYER UI:
//   GetCompanionCards(sort, filter) — all 26 companions sorted/filtered, ready to display
//   GetAssignmentSummary()          — current slot assignments as display strings
//   GetRuleCards()                  — all rules as readable text
//   GetSuggestions()                — top 3 companions resonating with player right now
//
// AI / NPC UI:
//   GetNpcCompanionCards(archetypeId) — which companions this NPC archetype has and needs

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpiritsCrossing.Companions
{
    // -------------------------------------------------------------------------
    // Sort modes for the companion list
    // -------------------------------------------------------------------------
    public enum CompanionSortMode
    {
        ByHarmony,    // most resonant first (live, changes every frame)
        ByElement,    // Air → Earth → Water → Fire, then by tier
        ByTier,       // Tier 3 (elders) first
        ByBondLevel,  // strongest bonds first
        ByName,       // alphabetical
    }

    // -------------------------------------------------------------------------
    // A single row in the companion list UI
    // -------------------------------------------------------------------------
    public struct CompanionCard
    {
        public string animalId;
        public string displayName;
        public string element;
        public int    tier;
        public float  bondLevel;       // 0-1 persistent bond
        public float  currentHarmony;  // 0-1 live resonance score
        public bool   isAssigned;      // in any assignment slot
        public string assignmentSlot;  // "primary" | "elemental_Air" | "realm_SkyRealm" | ""
        public string behaviorMode;    // from profile: "wise", "playful", etc.
        public string description;

        // UI colour: element-based hue, saturation = harmony, brightness = bond
        public Color CardColor()
        {
            float hue = element switch
            {
                "Air"   => 0.58f,   // blue
                "Earth" => 0.33f,   // green
                "Water" => 0.70f,   // indigo
                "Fire"  => 0.02f,   // red-orange
                _       => 0.50f,
            };
            return Color.HSVToRGB(hue, 0.4f + currentHarmony * 0.5f,
                                  0.5f + bondLevel * 0.4f);
        }
    }

    // -------------------------------------------------------------------------
    // Assignment slot display item
    // -------------------------------------------------------------------------
    public struct AssignmentSlotDisplay
    {
        public string slotId;       // "primary", "elemental_Air", "realm_SkyRealm", "session"
        public string slotLabel;    // "Primary Companion", "Air Companion", etc.
        public string animalId;     // assigned animal (null if empty)
        public string animalName;   // display name
        public float  bondLevel;
        public bool   isEmpty;
    }

    // -------------------------------------------------------------------------
    // A single rule in the UI rule list
    // -------------------------------------------------------------------------
    public struct RuleCard
    {
        public string ruleId;
        public string displayName;
        public string animalName;
        public string triggerText;   // e.g. "when calm > 0.70"
        public string actionText;    // e.g. "→ Approach"
        public bool   enabled;
        public int    priority;
    }

    // -------------------------------------------------------------------------
    // Main UI data provider — attach to any GameObject or use statically
    // -------------------------------------------------------------------------
    public class CompanionAssignmentUI : MonoBehaviour
    {
        public static CompanionAssignmentUI Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Companion cards — the main sortable/filterable list
        // -------------------------------------------------------------------------

        public List<CompanionCard> GetCompanionCards(
            CompanionSortMode sort = CompanionSortMode.ByHarmony,
            string elementFilter  = null)
        {
            var cards  = new List<CompanionCard>();
            var loader = CompanionBondSystem.Instance;
            if (loader == null || !loader.IsLoaded) return cards;

            var assignment = CompanionAssignmentManager.Instance?.Assignment;

            foreach (var profile in loader.AllProfiles)
            {
                if (!string.IsNullOrEmpty(elementFilter) && profile.element != elementFilter)
                    continue;

                float bond    = loader.GetBondLevel(profile.animalId);
                float harmony = VibrationalResonanceSystem.Instance?.GetHarmony(profile.animalId) ?? 0.5f;

                string slot   = GetAssignmentSlot(assignment, profile.animalId);
                bool assigned = !string.IsNullOrEmpty(slot);

                cards.Add(new CompanionCard
                {
                    animalId       = profile.animalId,
                    displayName    = profile.displayName,
                    element        = profile.element,
                    tier           = profile.tier,
                    bondLevel      = bond,
                    currentHarmony = harmony,
                    isAssigned     = assigned,
                    assignmentSlot = slot,
                    behaviorMode   = profile.behaviorMode,
                    description    = profile.description,
                });
            }

            SortCards(cards, sort);
            return cards;
        }

        private static void SortCards(List<CompanionCard> cards, CompanionSortMode mode)
        {
            switch (mode)
            {
                case CompanionSortMode.ByHarmony:
                    cards.Sort((a, b) => b.currentHarmony.CompareTo(a.currentHarmony));
                    break;
                case CompanionSortMode.ByElement:
                    cards.Sort((a, b) =>
                    {
                        int eq = ElementOrder(a.element).CompareTo(ElementOrder(b.element));
                        return eq != 0 ? eq : a.tier.CompareTo(b.tier);
                    });
                    break;
                case CompanionSortMode.ByTier:
                    cards.Sort((a, b) =>
                    {
                        int tq = b.tier.CompareTo(a.tier);
                        return tq != 0 ? tq : b.bondLevel.CompareTo(a.bondLevel);
                    });
                    break;
                case CompanionSortMode.ByBondLevel:
                    cards.Sort((a, b) => b.bondLevel.CompareTo(a.bondLevel));
                    break;
                case CompanionSortMode.ByName:
                    cards.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.Ordinal));
                    break;
            }
        }

        private static int ElementOrder(string e) => e switch
        { "Air" => 0, "Earth" => 1, "Water" => 2, "Fire" => 3, _ => 4 };

        // -------------------------------------------------------------------------
        // Assignment summary — what's in each slot
        // -------------------------------------------------------------------------

        public List<AssignmentSlotDisplay> GetAssignmentSummary()
        {
            var result     = new List<AssignmentSlotDisplay>();
            var assignment = CompanionAssignmentManager.Instance?.Assignment;
            var loader     = CompanionBondSystem.Instance;

            AddSlot(result, "primary",         "Primary Companion", assignment?.primaryCompanion,  loader);
            AddSlot(result, "elemental_Air",   "Air Companion",     assignment?.airCompanion,       loader);
            AddSlot(result, "elemental_Earth", "Earth Companion",   assignment?.earthCompanion,     loader);
            AddSlot(result, "elemental_Water", "Water Companion",   assignment?.waterCompanion,     loader);
            AddSlot(result, "elemental_Fire",  "Fire Companion",    assignment?.fireCompanion,      loader);
            AddSlot(result, "session",         "Session Companion", assignment?.sessionCompanion,   loader);

            // Realm overrides
            if (assignment != null)
                for (int i = 0; i < assignment.realmIds.Count; i++)
                    AddSlot(result, $"realm_{assignment.realmIds[i]}",
                            $"{assignment.realmIds[i]} Companion",
                            assignment.realmAnimals[i], loader);

            return result;
        }

        private static void AddSlot(List<AssignmentSlotDisplay> list, string slotId,
            string label, string animalId, CompanionBondSystem loader)
        {
            bool empty = string.IsNullOrEmpty(animalId);
            var profile = empty ? null : loader?.GetProfile(animalId);
            list.Add(new AssignmentSlotDisplay
            {
                slotId     = slotId,
                slotLabel  = label,
                animalId   = animalId,
                animalName = profile?.displayName ?? (empty ? "— empty —" : animalId),
                bondLevel  = empty ? 0f : (loader?.GetBondLevel(animalId) ?? 0f),
                isEmpty    = empty,
            });
        }

        // -------------------------------------------------------------------------
        // Rule cards — all rules as readable text
        // -------------------------------------------------------------------------

        public List<RuleCard> GetRuleCards()
        {
            var result = new List<RuleCard>();
            var rules  = CompanionAssignmentManager.Instance?.Rules;
            if (rules == null) return result;

            var loader = CompanionBondSystem.Instance;
            foreach (var rule in rules.rules)
            {
                string animalName = loader?.GetProfile(rule.animalId)?.displayName ?? rule.animalId;
                result.Add(new RuleCard
                {
                    ruleId      = rule.ruleId,
                    displayName = rule.displayName,
                    animalName  = animalName,
                    triggerText = FormatTrigger(rule),
                    actionText  = $"→ {rule.action}",
                    enabled     = rule.enabled,
                    priority    = rule.priority,
                });
            }
            return result;
        }

        private static string FormatTrigger(CompanionRule r) =>
            $"when {r.triggerDimension} {OperatorSymbol(r.triggerOperator)} {r.triggerThreshold:F2}";

        private static string OperatorSymbol(RuleTriggerOperator op) => op switch
        {
            RuleTriggerOperator.Above   => ">",
            RuleTriggerOperator.Below   => "<",
            RuleTriggerOperator.Rising  => "↑ rising past",
            RuleTriggerOperator.Falling => "↓ falling past",
            RuleTriggerOperator.Peak    => "peaks at",
            RuleTriggerOperator.Drop    => "drops below",
            _                           => "?"
        };

        // -------------------------------------------------------------------------
        // Suggestions — top 3 companions resonating right now
        // -------------------------------------------------------------------------

        public List<CompanionCard> GetSuggestions(int count = 3)
        {
            var cards = GetCompanionCards(CompanionSortMode.ByHarmony);
            // Prefer unassigned companions so suggestions feel fresh
            var suggestions = new List<CompanionCard>();
            foreach (var c in cards)
            {
                if (!c.isAssigned) suggestions.Add(c);
                if (suggestions.Count >= count) break;
            }
            // Fill with assigned if not enough unassigned
            if (suggestions.Count < count)
                foreach (var c in cards)
                {
                    if (suggestions.Count >= count) break;
                    if (!suggestions.Contains(c)) suggestions.Add(c);
                }
            return suggestions;
        }

        // -------------------------------------------------------------------------
        // NPC companion cards — what an AI player archetype has and needs
        // -------------------------------------------------------------------------

        public List<CompanionCard> GetNpcCompanionCards(string archetypeId)
        {
            var defaultIds = CompanionBondSystem.Instance?.GetNpcDefaultCompanions(archetypeId);
            if (defaultIds == null) return new List<CompanionCard>();

            var cards  = new List<CompanionCard>();
            var loader = CompanionBondSystem.Instance;
            foreach (var id in defaultIds)
            {
                var profile = loader?.GetProfile(id);
                if (profile == null) continue;
                cards.Add(new CompanionCard
                {
                    animalId       = id,
                    displayName    = profile.displayName,
                    element        = profile.element,
                    tier           = profile.tier,
                    bondLevel      = loader.GetBondLevel(id),
                    currentHarmony = VibrationalResonanceSystem.Instance?.GetHarmony(id) ?? 0.5f,
                    isAssigned     = true,
                    assignmentSlot = "npc_default",
                    behaviorMode   = profile.behaviorMode,
                    description    = profile.description,
                });
            }
            return cards;
        }

        // -------------------------------------------------------------------------
        // Active state summary string (for quick status display)
        // -------------------------------------------------------------------------

        public string GetActiveCompanionSummary()
        {
            var mgr = CompanionAssignmentManager.Instance;
            if (mgr == null) return "No manager";

            string active = mgr.ActiveCompanionId;
            if (string.IsNullOrEmpty(active)) return "No companion active";

            var profile = CompanionBondSystem.Instance?.GetProfile(active);
            float harmony = VibrationalResonanceSystem.Instance?.GetHarmony(active) ?? 0f;
            float bond    = CompanionBondSystem.Instance?.GetBondLevel(active) ?? 0f;

            return profile != null
                ? $"{profile.displayName} ({profile.element} T{profile.tier}) " +
                  $"harmony={harmony:F2} bond={bond:F2}"
                : active;
        }

        public int ActiveRuleCount()
            => CompanionAssignmentManager.Instance?.Rules?.rules?.Count ?? 0;

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private static string GetAssignmentSlot(CompanionAssignment a, string animalId)
        {
            if (a == null) return "";
            if (a.primaryCompanion  == animalId) return "primary";
            if (a.sessionCompanion  == animalId) return "session";
            if (a.airCompanion      == animalId) return "elemental_Air";
            if (a.earthCompanion    == animalId) return "elemental_Earth";
            if (a.waterCompanion    == animalId) return "elemental_Water";
            if (a.fireCompanion     == animalId) return "elemental_Fire";
            int idx = a.realmAnimals.IndexOf(animalId);
            return idx >= 0 ? $"realm_{a.realmIds[idx]}" : "";
        }

        // -------------------------------------------------------------------------
        // VibrationalResonanceSystem shortcut (avoids circular dependency)
        // -------------------------------------------------------------------------
        private static SpiritsCrossing.Vibration.VibrationalResonanceSystem VibrationalResonanceSystem
            => SpiritsCrossing.Vibration.VibrationalResonanceSystem.Instance;
    }
}
