using UnityEngine;
using SpiritsCrossing.Companions;

// CompanionInterpreter — reacts to companion bond tier changes and
// active companion state. Replaces the previous myth-polling approach.
// Subscribe to CompanionBondSystem events for responsive companion dialogue.
public class CompanionInterpreter : MonoBehaviour
{
    public MythEngine mythEngine;

    [Header("Current State")]
    public string activeCompanionId;
    public string activeCompanionElement;
    public CompanionBondTier activeTier = CompanionBondTier.Distant;

    private void OnEnable()
    {
        if (CompanionBondSystem.Instance == null) return;
        CompanionBondSystem.Instance.OnBondTierChanged      += OnBondTierChanged;
        CompanionBondSystem.Instance.OnCompanionFullyBonded += OnFullyBonded;
        CompanionBondSystem.Instance.OnActiveCompanionChanged += OnActiveCompanionChanged;
    }

    private void OnDisable()
    {
        if (CompanionBondSystem.Instance == null) return;
        CompanionBondSystem.Instance.OnBondTierChanged      -= OnBondTierChanged;
        CompanionBondSystem.Instance.OnCompanionFullyBonded -= OnFullyBonded;
        CompanionBondSystem.Instance.OnActiveCompanionChanged -= OnActiveCompanionChanged;
    }

    private void Update()
    {
        // Myth-reactive companion dialogue (existing behavior preserved)
        if (mythEngine == null) return;

        if (mythEngine.HasMyth("storm"))
            React("danger", "Companion senses storm energy on this path...");

        if (mythEngine.HasMyth("forest") && activeCompanionElement == "Earth")
            React("harmony", "Companion stirs — the forest remembers you.");

        if (mythEngine.HasMyth("elder") && activeTier >= CompanionBondTier.Bonded)
            React("elder", "Something ancient recognizes the bond between you.");
    }

    private void OnBondTierChanged(string animalId, CompanionBondTier tier)
    {
        var profile = CompanionBondSystem.Instance?.GetProfile(animalId);
        if (profile == null) return;

        activeTier             = tier;
        activeCompanionId      = animalId;
        activeCompanionElement = profile.element;

        Debug.Log($"[CompanionInterpreter] {profile.displayName} ({profile.element}) → {tier}");

        // Trigger dialogue based on tier
        switch (tier)
        {
            case CompanionBondTier.Curious:
                React("curious", $"{profile.displayName} draws closer — it senses something in you.");
                break;
            case CompanionBondTier.Bonded:
                React("bonded",  $"{profile.displayName} stays. The {profile.element} is with you now.");
                break;
            case CompanionBondTier.Companion:
                React("companion", $"{profile.displayName} is fully bonded. You share the {profile.element}.");
                break;
        }
    }

    private void OnFullyBonded(string animalId)
    {
        var profile = CompanionBondSystem.Instance?.GetProfile(animalId);
        if (profile == null) return;
        React("bond_complete",
              $"The {profile.displayName} has chosen you. {profile.description}");
    }

    private void OnActiveCompanionChanged(string animalId)
    {
        activeCompanionId = animalId;
        var profile = CompanionBondSystem.Instance?.GetProfile(animalId);
        activeCompanionElement = profile?.element ?? "";
    }

    private void React(string eventKey, string message)
    {
        // Extend this method to drive UI text, audio, animation, etc.
        Debug.Log($"[CompanionInterpreter] [{eventKey}] {message}");
    }
}
