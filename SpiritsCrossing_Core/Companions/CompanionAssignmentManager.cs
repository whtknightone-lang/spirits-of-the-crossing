// SpiritsCrossing — CompanionAssignmentManager.cs
// Runtime companion assignment and rule evaluation engine.
//
// PLAYER API:  AssignPrimary / AssignElemental / AssignRealm / AssignSession
//              AddRule / RemoveRule / LoadDefaultRules / ClearRules
//
// RULE ENGINE: Every frame, evaluates all enabled rules against the live
//              PlayerResonanceState using 6 operators:
//                Above / Below  — sustained threshold check
//                Rising / Falling — positive/negative delta
//                Peak / Drop    — single-frame edge (rising/falling crossing)
//              On fire: sends SetBehaviorOverride() to the matching
//              CompanionBehaviorController for the rule's actionDuration.
//
// NPC:         AutoAssignForNpc(archetypeId) assigns the default companion
//              for that archetype and applies element-specific starter rules.

using System;
using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.SpiritAI;
using SpiritsCrossing.Vibration;

namespace SpiritsCrossing.Companions
{
    public class CompanionAssignmentManager : MonoBehaviour
    {
        public static CompanionAssignmentManager Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Debug")]
        public bool logRuleFirings;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------
        public event Action<string>             OnAssignmentChanged; // slot name
        public event Action<CompanionRule, string> OnRuleTriggered;  // rule, animalId

        // -------------------------------------------------------------------------
        // Public state
        // -------------------------------------------------------------------------
        public CompanionAssignment  Assignment  => UniverseStateManager.Instance?.Current.userAssignment;
        public CompanionRuleSet     Rules       => UniverseStateManager.Instance?.Current.companionRules;

        public string ActiveCompanionId { get; private set; }

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private SpiritBrainOrchestrator _orchestrator;
        private readonly Dictionary<string, float> _prevValues = new();   // for delta tracking
        private readonly Dictionary<string, float> _overrideTimers = new(); // per-animalId override remaining

        private float _contextUpdateTimer;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();
        }

        private void Update()
        {
            if (_orchestrator == null)
                _orchestrator = FindObjectOfType<SpiritBrainOrchestrator>();

            EvaluateRules();
            TickOverrideTimers();

            // Refresh active companion context every 2s
            _contextUpdateTimer += Time.deltaTime;
            if (_contextUpdateTimer >= 2f)
            {
                _contextUpdateTimer = 0f;
                RefreshActiveCompanion();
            }
        }

        // -------------------------------------------------------------------------
        // Rule evaluation
        // -------------------------------------------------------------------------
        private void EvaluateRules()
        {
            var rules = Rules;
            if (rules == null) return;

            var state = _orchestrator?.CurrentPlayerState;
            if (state == null) return;

            float now = Time.time;
            foreach (var rule in rules.rules)
            {
                if (!rule.enabled) continue;
                if (now - rule.lastFiredTime < rule.cooldown) continue;

                float current = GetDimValue(state, rule.triggerDimension);
                float prev    = _prevValues.TryGetValue(rule.ruleId, out float p) ? p : current;
                float delta   = current - prev;

                bool fire = rule.triggerOperator switch
                {
                    RuleTriggerOperator.Above   => current > rule.triggerThreshold,
                    RuleTriggerOperator.Below   => current < rule.triggerThreshold,
                    RuleTriggerOperator.Rising  => delta > 0.002f,
                    RuleTriggerOperator.Falling => delta < -0.002f,
                    RuleTriggerOperator.Peak    => current >= rule.triggerThreshold && prev < rule.triggerThreshold,
                    RuleTriggerOperator.Drop    => current < rule.triggerThreshold  && prev >= rule.triggerThreshold,
                    _                           => false
                };

                _prevValues[rule.ruleId] = current;

                if (!fire) continue;

                rule.lastFiredTime = now;
                FireRule(rule);
            }
        }

        private void FireRule(CompanionRule rule)
        {
            // Find the companion's behavior controller
            CompanionBehaviorController ctrl = FindController(rule.animalId);
            if (ctrl == null) return;

            ctrl.SetBehaviorOverride(rule.action, rule.actionDuration);
            _overrideTimers[rule.animalId] = rule.actionDuration;

            OnRuleTriggered?.Invoke(rule, rule.animalId);
            if (logRuleFirings)
                Debug.Log($"[CompanionAssignmentManager] Rule fired: {rule.displayName} → {rule.action}");
        }

        private void TickOverrideTimers()
        {
            var keys = new List<string>(_overrideTimers.Keys);
            foreach (var id in keys)
            {
                _overrideTimers[id] -= Time.deltaTime;
                if (_overrideTimers[id] <= 0f) _overrideTimers.Remove(id);
            }
        }

        // -------------------------------------------------------------------------
        // Resolve active companion for current context
        // -------------------------------------------------------------------------
        private void RefreshActiveCompanion()
        {
            if (Assignment == null) { ActiveCompanionId = null; return; }

            // Get current realm and element from life cycle / realm system
            var universe = UniverseStateManager.Instance?.Current;
            string realmId  = universe?.lastRealmOutcome?.realmId  ?? "";
            string element  = universe?.learningState?.dominantElement ?? "";

            string resolved = Assignment.ResolveForContext(realmId, element);
            if (resolved != ActiveCompanionId)
            {
                ActiveCompanionId = resolved;
                if (!string.IsNullOrEmpty(resolved))
                    CompanionBondSystem.Instance?.SetActiveCompanion(resolved);
            }
        }

        // -------------------------------------------------------------------------
        // Assignment API
        // -------------------------------------------------------------------------
        public void AssignPrimary(string animalId)
        {
            EnsureAssignmentExists();
            Assignment.SetPrimary(animalId);
            Save(); OnAssignmentChanged?.Invoke("primary");
            Debug.Log($"[CompanionAssignmentManager] Primary → {animalId}");
        }

        public void AssignElemental(string element, string animalId)
        {
            EnsureAssignmentExists();
            Assignment.SetElemental(element, animalId);
            Save(); OnAssignmentChanged?.Invoke($"elemental_{element}");
        }

        public void AssignRealm(string realmId, string animalId)
        {
            EnsureAssignmentExists();
            Assignment.SetRealm(realmId, animalId);
            Save(); OnAssignmentChanged?.Invoke($"realm_{realmId}");
        }

        public void AssignSession(string animalId)
        {
            EnsureAssignmentExists();
            Assignment.sessionCompanion = animalId;
            Save(); OnAssignmentChanged?.Invoke("session");
        }

        public void ClearSlot(string slot)
        {
            if (Assignment == null) return;
            switch (slot)
            {
                case "primary": Assignment.primaryCompanion = null; break;
                case "session": Assignment.sessionCompanion = null; break;
                default:
                    if (slot.StartsWith("elemental_")) Assignment.SetElemental(slot.Substring(10), null);
                    else if (slot.StartsWith("realm_"))   Assignment.ClearRealm(slot.Substring(6));
                    break;
            }
            Save(); OnAssignmentChanged?.Invoke(slot);
        }

        // -------------------------------------------------------------------------
        // Rule API
        // -------------------------------------------------------------------------
        public void AddRule(CompanionRule rule)
        {
            EnsureRuleSetExists();
            Rules.AddRule(rule);
            Save();
        }

        public void AddQuickRule(string animalId, string dimension,
            RuleTriggerOperator op, float threshold, CompanionRuleAction action,
            string displayName = null)
        {
            EnsureRuleSetExists();
            Rules.AddQuickRule(animalId, dimension, op, threshold, action, displayName);
            Save();
        }

        public bool RemoveRule(string ruleId)
        {
            if (Rules == null) return false;
            bool removed = Rules.RemoveRule(ruleId);
            if (removed) Save();
            return removed;
        }

        public void ClearRules()
        {
            Rules?.Clear();
            Save();
        }

        public void EnableRule(string ruleId, bool enabled)
        {
            var rule = Rules?.GetRule(ruleId);
            if (rule == null) return;
            rule.enabled = enabled;
            Save();
        }

        /// <summary>
        /// Load default starter rules for the player's primary companion.
        /// Derived from the companion's element — gives natural organic behavior.
        /// </summary>
        public void LoadDefaultRules(string animalId)
        {
            var profile = CompanionBondSystem.Instance?.GetProfile(animalId);
            if (profile == null) return;

            EnsureRuleSetExists();
            var defaults = CompanionRuleSet.DefaultRulesFor(animalId, profile.element);
            foreach (var r in defaults.rules) Rules.AddRule(r);
            Save();
            Debug.Log($"[CompanionAssignmentManager] Default rules loaded for {animalId} ({profile.element})");
        }

        // -------------------------------------------------------------------------
        // NPC auto-assignment
        // -------------------------------------------------------------------------

        /// <summary>
        /// Automatically assign companions and rules for an NPC spirit archetype.
        /// Called when a spirit initializes in the scene.
        /// </summary>
        public (string primaryId, CompanionRuleSet rules) AutoAssignForNpc(string archetypeId)
        {
            var defaultIds = CompanionBondSystem.Instance?.GetNpcDefaultCompanions(archetypeId);
            if (defaultIds == null || defaultIds.Count == 0)
                return (null, new CompanionRuleSet());

            string primaryId = defaultIds[0];
            var profile = CompanionBondSystem.Instance?.GetProfile(primaryId);
            string element  = profile?.element ?? "Air";

            var rules = CompanionRuleSet.DefaultRulesFor(primaryId, element);

            // If a second companion exists, add rules for it too
            if (defaultIds.Count > 1)
            {
                string secondId      = defaultIds[1];
                var secondProfile    = CompanionBondSystem.Instance?.GetProfile(secondId);
                string secondElement = secondProfile?.element ?? element;
                var secondRules      = CompanionRuleSet.DefaultRulesFor(secondId, secondElement);
                foreach (var r in secondRules.rules) rules.AddRule(r);
            }

            if (logRuleFirings)
                Debug.Log($"[CompanionAssignmentManager] NPC {archetypeId} auto-assigned: " +
                          $"{primaryId} + {rules.rules.Count} rules");

            return (primaryId, rules);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------
        private static float GetDimValue(PlayerResonanceState s, string dim) => dim switch
        {
            "calm"            => s.calm,
            "joy"             => s.joy,
            "wonder"          => s.wonder,
            "socialSync"      => s.socialSync,
            "movementFlow"    => s.movementFlow,
            "spinStability"   => s.spinStability,
            "sourceAlignment" => s.sourceAlignment,
            "breathCoherence" => s.breathCoherence,
            "distortion"      => s.distortion,
            _                 => 0f
        };

        private static CompanionBehaviorController FindController(string animalId)
        {
            foreach (var c in FindObjectsOfType<CompanionBehaviorController>())
                if (c.animalId == animalId) return c;
            return null;
        }

        private void EnsureAssignmentExists()
        {
            var u = UniverseStateManager.Instance?.Current;
            if (u != null && u.userAssignment == null)
                u.userAssignment = new CompanionAssignment();
        }

        private void EnsureRuleSetExists()
        {
            var u = UniverseStateManager.Instance?.Current;
            if (u != null && u.companionRules == null)
                u.companionRules = new CompanionRuleSet();
        }

        private void Save() => UniverseStateManager.Instance?.Save();
    }
}
