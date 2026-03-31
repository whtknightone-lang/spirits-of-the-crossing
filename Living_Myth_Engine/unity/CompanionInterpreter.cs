using UnityEngine;
using SpiritsCrossing.Companions;
using SpiritsCrossing.Vibration;

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

        if (EmotionalFieldPropagation.Instance != null)
        {
            EmotionalFieldPropagation.Instance.OnSpectrumShift     += OnEmotionalSpectrumShift;
            EmotionalFieldPropagation.Instance.OnFullSpectrumReached += OnFullSpectrumReached;
            EmotionalFieldPropagation.Instance.OnDepthThreshold     += OnEmotionalDepthThreshold;
        }
    }

    private void OnDisable()
    {
        if (CompanionBondSystem.Instance == null) return;
        CompanionBondSystem.Instance.OnBondTierChanged      -= OnBondTierChanged;
        CompanionBondSystem.Instance.OnCompanionFullyBonded -= OnFullyBonded;
        CompanionBondSystem.Instance.OnActiveCompanionChanged -= OnActiveCompanionChanged;

        if (EmotionalFieldPropagation.Instance != null)
        {
            EmotionalFieldPropagation.Instance.OnSpectrumShift     -= OnEmotionalSpectrumShift;
            EmotionalFieldPropagation.Instance.OnFullSpectrumReached -= OnFullSpectrumReached;
            EmotionalFieldPropagation.Instance.OnDepthThreshold     -= OnEmotionalDepthThreshold;
        }
    }

    private void Update()
    {
        // Myth-reactive companion dialogue — gated by companionSensitivity
        if (mythEngine == null) return;

        float sensitivity = mythEngine.GetMythStrength("elder") +
                            mythEngine.GetMythStrength("source") * 0.5f;
        // Read companionSensitivity from MythState if UniverseStateManager is available
        var mythState = SpiritsCrossing.UniverseStateManager.Instance?.Current?.mythState;
        if (mythState != null)
            sensitivity = mythState.companionSensitivity;

        // Only trigger myth-reactive dialogue when sensitivity exceeds threshold
        if (sensitivity <= 0.3f) return;

        // Determine age tier for dialogue variant selection
        var ageTier = SpiritsCrossing.UniverseStateManager.Instance?.Current?.ageTier
                      ?? SpiritsCrossing.AgeTier.Voyager;
        bool isSeedling = ageTier == SpiritsCrossing.AgeTier.Seedling;

        // Scale urgency: low sensitivity = gentle hints, high = urgent presence
        bool urgent = !isSeedling && sensitivity >= 0.7f;

        if (mythEngine.HasMyth("storm"))
            React("danger", urgent
                ? "The air crackles — your companion presses close, warning of storm."
                : "Companion senses storm energy on this path...");

        if (mythEngine.HasMyth("forest") && activeCompanionElement == "Earth")
            React("harmony", urgent
                ? "The ground hums beneath you both — the forest knows your name."
                : "Companion stirs — the forest remembers you.");

        if (mythEngine.HasMyth("elder") && activeTier >= CompanionBondTier.Bonded)
            React("elder", urgent
                ? "An elder presence fills the space between you. The bond deepens."
                : "Something ancient recognizes the bond between you.");

        if (mythEngine.HasMyth("harmony"))
            React("harmony_all", urgent
                ? "All four elements resonate through your companion — the world listens."
                : "Your companion feels the balance of all elements.");

        // --- Young AI learning dialogue ---
        if (mythEngine.HasMyth("discovery"))
            React("ai_discovery", isSeedling
                ? "I just learned something new from watching you! Look!"
                : "The spirit noticed a pattern in your resonance it hadn't seen before.");

        if (mythEngine.HasMyth("insight"))
            React("ai_insight", isSeedling
                ? "Ooh! I figured something out! You keep doing that same beautiful thing!"
                : "An insight forms — the AI spirit shares what it discovered about your style.");

        if (mythEngine.HasMyth("exploration"))
            React("ai_exploration", isSeedling
                ? "Come on, come on! There's something new over here! I can smell it!"
                : "The spirit scouts ahead, beckoning you toward unexplored territory.");

        if (mythEngine.HasMyth("wonder"))
            React("wonder", isSeedling
                ? "Everything is so sparkly right now! Do you see it too?"
                : "Wonder fills the space between you and your companion.");

        if (mythEngine.HasMyth("friendship"))
            React("friendship", isSeedling
                ? "I like being with you. The world likes it too."
                : "The bond warms. Something in the realm responds to the connection.");
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

    // ----------------------------------------------------------------
    // Emotional spectrum reactions
    // ----------------------------------------------------------------

    private void OnEmotionalSpectrumShift(string fromBand, string toBand)
    {
        // Determine age tier for dialogue variant selection
        var ageTier = SpiritsCrossing.UniverseStateManager.Instance?.Current?.ageTier
                      ?? SpiritsCrossing.AgeTier.Voyager;
        bool isSeedling = ageTier == SpiritsCrossing.AgeTier.Seedling;

        string message = toBand switch
        {
            "stillness" => isSeedling
                ? "Shh... I feel quiet now. Like the ground is listening."
                : "The companion settles. Stillness rises between you.",
            "peace" => isSeedling
                ? "Everything feels soft. Like breathing underwater."
                : "A wave of peace passes through the bond.",
            "joy" => isSeedling
                ? "I feel sparkly! Do you feel sparkly too?!"
                : "Joy floods the connection — the companion brightens.",
            "love" => isSeedling
                ? "My heart is so big right now. It feels like sunshine."
                : "Love radiates through the bond. The world notices.",
            "source" => isSeedling
                ? "Whoa... everything is everywhere. I can feel the whole sky."
                : "Source alignment deepens. The companion goes still with reverence.",
            _ => null
        };

        if (message != null)
            React($"emotion_{toBand}", message);
    }

    private void OnFullSpectrumReached(float coherence)
    {
        var ageTier = SpiritsCrossing.UniverseStateManager.Instance?.Current?.ageTier
                      ?? SpiritsCrossing.AgeTier.Voyager;
        bool isSeedling = ageTier == SpiritsCrossing.AgeTier.Seedling;

        React("full_spectrum", isSeedling
            ? "EVERYTHING is singing! All of it! All at once! Can you hear it?!"
            : "All five frequencies align. Stillness, peace, joy, love, source — " +
              "the full spectrum resonates through the bond. The world holds its breath.");
    }

    private void OnEmotionalDepthThreshold(int level, float depth)
    {
        var ageTier = SpiritsCrossing.UniverseStateManager.Instance?.Current?.ageTier
                      ?? SpiritsCrossing.AgeTier.Voyager;
        bool isSeedling = ageTier == SpiritsCrossing.AgeTier.Seedling;

        string message = level switch
        {
            0 => isSeedling
                ? "I'm starting to feel something deep. Like the ground humming."
                : "Emotional depth stirs. The companion leans closer.",
            1 => isSeedling
                ? "We're going deeper! I can feel colors I've never seen!"
                : "The spectrum deepens. Resonance thickens around you both.",
            2 => isSeedling
                ? "This is the deepest I've ever been. Are you still here? ...Good."
                : "Profound depth. The companion's presence becomes radiant.",
            3 => isSeedling
                ? "I think... I think I can feel everything that ever was."
                : "Maximum emotional depth. The boundary between you and the world dissolves.",
            _ => null
        };

        if (message != null)
            React($"depth_{level}", message);
    }

    private void React(string eventKey, string message)
    {
        // Extend this method to drive UI text, audio, animation, etc.
        Debug.Log($"[CompanionInterpreter] [{eventKey}] {message}");
    }
}
