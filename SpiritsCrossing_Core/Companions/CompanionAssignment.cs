// SpiritsCrossing — CompanionAssignment.cs
// Data types for the user-controlled companion assignment and rules system.
//
// Assignment types:
//   Primary    — one companion that follows everywhere, always active
//   Elemental  — one companion per element, active when in matching element scenes
//   Realm      — one companion per named realm (more specific override)
//   Session    — active only during a cave ritual session
//
// Rules: player-defined trigger → action pairs.
//   When the player's resonance matches a rule's trigger, the rule's target
//   companion is instructed to perform a specific action.
//
//   Example rules:
//     "When calm > 0.70  → snake approaches"
//     "When distortion > 0.55 → panther retreats"
//     "When sourceAlignment peaks above 0.65 → whale performs"
//     "When wonder rising → raven alerts"

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpiritsCrossing.Companions
{
    // -------------------------------------------------------------------------
    // Rule trigger operators
    // -------------------------------------------------------------------------
    public enum RuleTriggerOperator
    {
        Above,    // dimension value > threshold (sustained)
        Below,    // dimension value < threshold (sustained)
        Rising,   // dimension is increasing (positive delta this frame)
        Falling,  // dimension is decreasing (negative delta this frame)
        Peak,     // dimension just crossed threshold from below (rising edge only)
        Drop,     // dimension just crossed threshold from above (falling edge only)
    }

    // -------------------------------------------------------------------------
    // Rule actions — what the companion does when the rule fires
    // -------------------------------------------------------------------------
    public enum CompanionRuleAction
    {
        Approach,   // move to bonded distance (override tier)
        Retreat,    // move to distant/curious distance
        Alert,      // trigger Alert animation, face player
        Perform,    // trigger element-specific performance animation
        Bond,       // temporarily increase bond growth rate
        Guard,      // position between player and current threat direction
        Follow,     // tight following, stays very close
        Release,    // deactivate / dismiss the companion for this session
    }

    // -------------------------------------------------------------------------
    // A single rule
    // -------------------------------------------------------------------------
    [Serializable]
    public class CompanionRule
    {
        public string             ruleId;           // unique id e.g. "calm_snake"
        public string             displayName;      // human-readable label
        public string             animalId;         // target companion
        public string             triggerDimension; // "calm"|"joy"|"wonder"|"sourceAlignment" etc.
        public RuleTriggerOperator triggerOperator;
        [Range(0f, 1f)]
        public float              triggerThreshold;
        public CompanionRuleAction action;
        [Range(1, 10)]
        public int                priority = 5;     // higher = evaluated first
        public bool               enabled  = true;

        [Tooltip("Seconds the action sustains after the rule fires. 0 = single frame.")]
        public float              actionDuration = 3.0f;

        [Tooltip("Minimum seconds between firings of this rule.")]
        public float              cooldown       = 5.0f;

        // Runtime-only (not serialized to save)
        [NonSerialized] public float lastFiredTime = -99f;
        [NonSerialized] public bool  prevAboveThreshold;
    }

    // -------------------------------------------------------------------------
    // The player's full rule set
    // -------------------------------------------------------------------------
    [Serializable]
    public class CompanionRuleSet
    {
        public List<CompanionRule> rules = new List<CompanionRule>();

        public void AddRule(CompanionRule rule)
        {
            // Ensure unique id
            rules.RemoveAll(r => r.ruleId == rule.ruleId);
            rules.Add(rule);
            // Sort by priority descending
            rules.Sort((a, b) => b.priority.CompareTo(a.priority));
        }

        public bool RemoveRule(string ruleId)
        {
            int before = rules.Count;
            rules.RemoveAll(r => r.ruleId == ruleId);
            return rules.Count < before;
        }

        public CompanionRule GetRule(string ruleId)
        {
            foreach (var r in rules) if (r.ruleId == ruleId) return r;
            return null;
        }

        public void Clear() => rules.Clear();

        /// <summary>
        /// Convenience: create a rule with common parameters.
        /// </summary>
        public CompanionRule AddQuickRule(string animalId, string dimension,
            RuleTriggerOperator op, float threshold, CompanionRuleAction action,
            string displayName = null, float duration = 3f, float cooldown = 5f)
        {
            string id = $"{animalId}_{dimension}_{op}".ToLower().Replace(" ", "_");
            var rule = new CompanionRule
            {
                ruleId           = id,
                displayName      = displayName ?? $"{animalId}: {dimension} {op} {threshold:F2}",
                animalId         = animalId,
                triggerDimension = dimension,
                triggerOperator  = op,
                triggerThreshold = threshold,
                action           = action,
                actionDuration   = duration,
                cooldown         = cooldown,
                enabled          = true,
            };
            AddRule(rule);
            return rule;
        }

        /// <summary>Built-in starter rules for a given archetype assignment.</summary>
        public static CompanionRuleSet DefaultRulesFor(string animalId, string element)
        {
            var rs = new CompanionRuleSet();

            switch (element)
            {
                case "Earth":
                    rs.AddQuickRule(animalId, "calm",            RuleTriggerOperator.Above, 0.65f,  CompanionRuleAction.Approach,  $"{animalId} comes when you are still");
                    rs.AddQuickRule(animalId, "sourceAlignment", RuleTriggerOperator.Peak,  0.60f,  CompanionRuleAction.Perform,   $"{animalId} performs at source peak");
                    rs.AddQuickRule(animalId, "distortion",      RuleTriggerOperator.Above, 0.55f,  CompanionRuleAction.Retreat,   $"{animalId} retreats from distortion");
                    break;

                case "Air":
                    rs.AddQuickRule(animalId, "wonder",          RuleTriggerOperator.Rising, 0.50f, CompanionRuleAction.Alert,    $"{animalId} stirs when wonder rises");
                    rs.AddQuickRule(animalId, "spinStability",   RuleTriggerOperator.Above,  0.65f, CompanionRuleAction.Follow,   $"{animalId} follows spinning motion");
                    rs.AddQuickRule(animalId, "calm",            RuleTriggerOperator.Below,  0.25f, CompanionRuleAction.Retreat,  $"{animalId} pulls away from restlessness");
                    break;

                case "Water":
                    rs.AddQuickRule(animalId, "movementFlow",    RuleTriggerOperator.Above,  0.60f, CompanionRuleAction.Follow,   $"{animalId} flows with your movement");
                    rs.AddQuickRule(animalId, "sourceAlignment", RuleTriggerOperator.Peak,   0.55f, CompanionRuleAction.Perform,  $"{animalId} sings at source peak");
                    rs.AddQuickRule(animalId, "joy",             RuleTriggerOperator.Rising, 0.45f, CompanionRuleAction.Approach, $"{animalId} drawn by rising joy");
                    break;

                case "Fire":
                    rs.AddQuickRule(animalId, "spinStability",   RuleTriggerOperator.Above,  0.70f, CompanionRuleAction.Perform,  $"{animalId} responds to spin");
                    rs.AddQuickRule(animalId, "distortion",      RuleTriggerOperator.Drop,   0.40f, CompanionRuleAction.Alert,    $"{animalId} alerts when distortion clears");
                    rs.AddQuickRule(animalId, "calm",            RuleTriggerOperator.Above,  0.75f, CompanionRuleAction.Guard,    $"{animalId} guards in deep calm");
                    break;
            }
            return rs;
        }
    }

    // -------------------------------------------------------------------------
    // The player's companion assignments across all contexts
    // -------------------------------------------------------------------------
    [Serializable]
    public class CompanionAssignment
    {
        // One companion that follows everywhere — the player's signature companion
        public string primaryCompanion;

        // One companion per element — active when scene element matches
        public string airCompanion;
        public string earthCompanion;
        public string waterCompanion;
        public string fireCompanion;

        // Realm-specific overrides (more specific than element)
        // Stored as flat parallel lists for JsonUtility compatibility
        public List<string> realmIds     = new List<string>();
        public List<string> realmAnimals = new List<string>();

        // Cave session companion — active only during ritual
        public string sessionCompanion;

        public string lastModifiedUtc;

        // -------------------------------------------------------------------------
        // Assignment helpers
        // -------------------------------------------------------------------------
        public void SetPrimary(string animalId)
        {
            primaryCompanion = animalId;
            lastModifiedUtc  = DateTime.UtcNow.ToString("o");
        }

        public void SetElemental(string element, string animalId)
        {
            switch (element)
            {
                case "Air":   airCompanion   = animalId; break;
                case "Earth": earthCompanion = animalId; break;
                case "Water": waterCompanion = animalId; break;
                case "Fire":  fireCompanion  = animalId; break;
            }
            lastModifiedUtc = DateTime.UtcNow.ToString("o");
        }

        public string GetElemental(string element) => element switch
        {
            "Air"   => airCompanion,
            "Earth" => earthCompanion,
            "Water" => waterCompanion,
            "Fire"  => fireCompanion,
            _       => null
        };

        public void SetRealm(string realmId, string animalId)
        {
            int idx = realmIds.IndexOf(realmId);
            if (idx >= 0) realmAnimals[idx] = animalId;
            else { realmIds.Add(realmId); realmAnimals.Add(animalId); }
            lastModifiedUtc = DateTime.UtcNow.ToString("o");
        }

        public string GetRealm(string realmId)
        {
            int idx = realmIds.IndexOf(realmId);
            return idx >= 0 ? realmAnimals[idx] : null;
        }

        public void ClearRealm(string realmId)
        {
            int idx = realmIds.IndexOf(realmId);
            if (idx >= 0) { realmIds.RemoveAt(idx); realmAnimals.RemoveAt(idx); }
        }

        /// <summary>
        /// Resolve which companion should be active given a current scene context.
        /// Priority: realm > element > primary.
        /// </summary>
        public string ResolveForContext(string realmId, string element)
        {
            string realm = GetRealm(realmId);
            if (!string.IsNullOrEmpty(realm))   return realm;
            string elem = GetElemental(element);
            if (!string.IsNullOrEmpty(elem))     return elem;
            return primaryCompanion;
        }

        public bool HasAnyAssignment() =>
            !string.IsNullOrEmpty(primaryCompanion)  ||
            !string.IsNullOrEmpty(airCompanion)       ||
            !string.IsNullOrEmpty(earthCompanion)     ||
            !string.IsNullOrEmpty(waterCompanion)     ||
            !string.IsNullOrEmpty(fireCompanion);
    }
}
