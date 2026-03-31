using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PortalIntelligence — selects the best realm destination using calibrated
/// portal bias values from MythState rather than binary myth checks.
/// When no myth biases are active, falls back to the player's highest-affinity planet.
/// </summary>
public class PortalIntelligence : MonoBehaviour
{
    public MythEngine mythEngine;

    // Realm keys mapped to their world scene names
    private static readonly (string realmKey, string worldName)[] Realms =
    {
        ("forest",  "VerdantWorld"),
        ("sky",     "SkyWorld"),
        ("ocean",   "OceanWorld"),
        ("fire",    "FireWorld"),
        ("machine", "MachineWorld"),
        ("source",  "SourceWorld"),
    };

    /// <summary>
    /// Returns the best destination world name based on current myth portal biases.
    /// </summary>
    public string GetDestination()
    {
        if (mythEngine == null) return "UnknownRealm";

        string bestWorld  = null;
        float  bestBias   = 0f;

        foreach (var (realmKey, worldName) in Realms)
        {
            float bias = mythEngine.GetPortalBias(realmKey);
            if (bias > bestBias)
            {
                bestBias  = bias;
                bestWorld = worldName;
            }
        }

        // If any myth bias is active, use it
        if (bestWorld != null && bestBias > 0f)
            return bestWorld;

        // Fallback: player's highest-affinity planet from persistent state
        return GetHighestAffinityWorld();
    }

    /// <summary>
    /// Returns all realms ranked by portal bias (strongest first).
    /// Useful for presenting multiple portal choices to the player.
    /// </summary>
    public List<(string worldName, float bias)> GetRankedDestinations()
    {
        var ranked = new List<(string, float)>();
        if (mythEngine == null) return ranked;

        foreach (var (realmKey, worldName) in Realms)
        {
            float bias = mythEngine.GetPortalBias(realmKey);
            if (bias > 0f)
                ranked.Add((worldName, bias));
        }

        ranked.Sort((a, b) => b.bias.CompareTo(a.bias));
        return ranked;
    }

    private string GetHighestAffinityWorld()
    {
        var universe = SpiritsCrossing.UniverseStateManager.Instance?.Current;
        if (universe == null || universe.planets.Count == 0)
            return "UnknownRealm";

        SpiritsCrossing.PlanetState best = null;
        foreach (var p in universe.planets)
        {
            if (!p.unlocked) continue;
            if (best == null || p.affinityScore > best.affinityScore)
                best = p;
        }

        return best != null ? PlanetIdToWorldName(best.planetId) : "UnknownRealm";
    }

    private static string PlanetIdToWorldName(string planetId) => planetId?.ToLower() switch
    {
        "forestheart"  => "VerdantWorld",
        "skyspiral"    => "SkyWorld",
        "waterflow"    => "OceanWorld",
        "machineorder" => "MachineWorld",
        "sourceveil"   => "SourceWorld",
        _              => "UnknownRealm"
    };
}
