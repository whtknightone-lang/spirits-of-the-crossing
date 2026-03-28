using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing;

// MythEngine — scene-level facade over the persistent MythState owned by
// UniverseStateManager. Provides the original HasMyth / RegisterMyth API
// so existing callers (CompanionInterpreter, PortalIntelligence) need no
// changes, while all actual state lives in the persistent cosmos layer.
public class MythEngine : MonoBehaviour
{
    // Legacy inspector list kept for read-only debug visibility in the editor.
    // Do not write to this directly; use RegisterMyth() instead.
    [SerializeField, Tooltip("Read-only debug view. State is owned by UniverseStateManager.")]
    private List<string> activeMythKeys = new List<string>();

    private MythState MythState =>
        UniverseStateManager.Instance?.Current?.mythState;

    private void OnEnable()
    {
        if (UniverseStateManager.Instance != null)
            UniverseStateManager.Instance.OnSessionApplied += RefreshDebugView;
    }

    private void OnDisable()
    {
        if (UniverseStateManager.Instance != null)
            UniverseStateManager.Instance.OnSessionApplied -= RefreshDebugView;
    }

    /// <summary>Activate a myth by key. Delegates to persistent MythState.</summary>
    public void RegisterMyth(string myth, float strength = 0.7f)
    {
        if (MythState != null)
        {
            MythState.Activate(myth, "scene", strength);
            RefreshDebugView(null);
            Debug.Log($"[MythEngine] Myth activated: {myth} (strength={strength:F2})");
        }
        else
        {
            // Fallback if UniverseStateManager not yet initialised (e.g. editor test)
            if (!activeMythKeys.Contains(myth))
            {
                activeMythKeys.Add(myth);
                Debug.Log($"[MythEngine] Myth activated (local fallback): {myth}");
            }
        }
    }

    /// <summary>Check whether a myth key is currently active.</summary>
    public bool HasMyth(string myth)
    {
        if (MythState != null)
            return MythState.HasMyth(myth);

        // Fallback
        return activeMythKeys.Contains(myth);
    }

    /// <summary>Return the 0–1 strength of an active myth (0 if inactive).</summary>
    public float GetMythStrength(string myth)
    {
        return MythState?.GetStrength(myth) ?? 0f;
    }

    /// <summary>Read portal bias modifier for a given realm key.</summary>
    public float GetPortalBias(string realmKey) => realmKey switch
    {
        "forest"  => MythState?.portalBiasForest  ?? 0f,
        "sky"     => MythState?.portalBiasSky     ?? 0f,
        "ocean"   => MythState?.portalBiasOcean   ?? 0f,
        "fire"    => MythState?.portalBiasFire     ?? 0f,
        "machine" => MythState?.portalBiasMachine  ?? 0f,
        "source"  => MythState?.portalBiasSource   ?? 0f,
        _         => 0f
    };

    private void RefreshDebugView(SessionResonanceResult _)
    {
        activeMythKeys.Clear();
        if (MythState == null) return;
        foreach (var m in MythState.activeMyths)
            activeMythKeys.Add($"{m.mythKey} ({m.strength:F2})");
    }
}
