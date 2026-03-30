// Spirits of the Crossing — RUE World State Models
// Data classes that match the Python server's JSON responses exactly.
// Used by RUEBridge to deserialise world state from the simulation engine.

using System;
using System.Collections.Generic;

namespace SpiritsCrossing.RUE
{
    [Serializable]
    public class PlayerResonance
    {
        public float sync_level      = 0f;
        public float energy          = 2f;
        public string archetype      = null;   // e.g. "FlowDancer"
        public string planet_affinity = null;  // e.g. "SourceVeil"
        public float breath_phase    = 0f;     // 0 to 2π
    }

    [Serializable]
    public class PlanetState
    {
        public int    index;
        public string name;
        public string element;
        public float  energy;
        public string myth_summary;
        public string myth_tier;       // "seedling", "explorer", "voyager", or null
        public List<string> active_myths;
        public float  pos_x;
        public float  pos_y;

        /// <summary>True if any myth is active at explorer or voyager tier.</summary>
        public bool IsAwakened => myth_tier == "explorer" || myth_tier == "voyager";
    }

    [Serializable]
    public class NodeState
    {
        public int    planet_id;
        public string planet_name;
        public float  frequency;
        public float  amplitude;
        public float  phase;
        public float  pos_x;
        public float  pos_y;
    }

    [Serializable]
    public class AgentSummary
    {
        public string archetype;
        public string planet_name;
        public float  energy;
        public float  brain_sync;
        public float  field_alignment;
    }

    [Serializable]
    public class WorldState
    {
        public int   age;
        public int   step;
        public float star_energy;
        public float global_sync;
        public float global_energy;
        public float core_energy;
        public float shell_coherence;
        public float mayan_cycle_progress;   // 0.0 → 1.0 within current Long Count cycle

        public List<PlanetState>  planets;
        public List<NodeState>    upsilon_nodes;
        public List<AgentSummary> top_agents;

        /// <summary>Find planet by name.</summary>
        public PlanetState GetPlanet(string name)
        {
            return planets?.Find(p => p.name == name);
        }
    }
}
