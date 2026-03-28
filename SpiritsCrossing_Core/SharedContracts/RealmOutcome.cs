// SpiritsCrossing — RealmOutcome.cs
// Contract emitted by every IRealmController (e.g. ElderAirDragonGameLoop)
// when a realm session ends. Consumed by UniverseStateManager to update
// planet history, myth state, and universe cycle.

using System;
using System.Collections.Generic;

namespace SpiritsCrossing
{
    [Serializable]
    public class RealmOutcome
    {
        public string realmId;
        public string planetId;

        // Quality axes that map to PlanetNodeController.RecordEncounter
        public float celebration;   // high harmony/flow/joy → high celebration
        public float contrast;      // high distortion/storm → high contrast
        public float importance;    // overall activity (resonance * time) → importance

        // Final per-realm metrics (realm-specific, normalized 0–1)
        public float harmonyFinal;
        public float resonanceFinal;
        public float skyFlowFinal;   // SkyRealm-specific; 0 in other realms

        // Myth triggers discovered during the realm visit
        public List<string> mythTriggerKeys = new List<string>();

        // Whether the player reached a meaningful completion threshold
        public bool realmCompleted;

        public string utcTimestamp;

        public static RealmOutcome Empty(string realmId, string planetId) =>
            new RealmOutcome
            {
                realmId      = realmId,
                planetId     = planetId,
                utcTimestamp = DateTime.UtcNow.ToString("o")
            };
    }
}
