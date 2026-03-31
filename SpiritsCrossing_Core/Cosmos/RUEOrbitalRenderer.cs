// SpiritsCrossing — RUEOrbitalRenderer.cs
// Live Unity equivalent of rue-engine/engine/rendering/simple_renderer.py
//
// RUE Python:
//   class SimpleRenderer:
//       def render_universe(self, universe, output_path):
//           for planet in universe.planets:
//               x, y = planet.position                         # position_from_polar(r, θ)
//               orbit = Circle((0, 0), planet.radius)
//               ax.scatter([x], [y])
//
//   orbit.py:
//       def position_from_polar(radius, angle):
//           return radius * cos(angle), radius * sin(angle)
//
// This MonoBehaviour replaces the static matplotlib snapshot with a live
// Unity scene update. On a configurable interval it:
//   1. Reads each planet's live orbital angle from PlanetAutonomySystem
//   2. Reads its orbital radius from CosmosGenerationSystem cosmos data
//   3. Computes world position: x = r * cos(θ), z = r * sin(θ)  (Unity XZ plane)
//      scaled by unitsPerOrbitalUnit (designer-configurable)
//   4. Moves the matching PlanetNodeController GameObject Transform
//   5. Calls PlanetNodeController.Refresh() so visual state is current
//
// SETUP
//   Place ONE RUEOrbitalRenderer on any persistent GameObject in the CosmosMap scene.
//   Planet GameObjects must be named exactly after their planetId (e.g. "ForestHeart")
//   and carry a PlanetNodeController component — the same requirement as
//   CosmosGenerationSystem.SeedPlanetControllers().
//
//   The source (star) sits at world origin (0, starY, 0).
//   unitsPerOrbitalUnit converts RUE orbital radius units to Unity world units.
//   A typical cosmos map might use 10–30 Unity units per orbital radius unit.

using System.Collections.Generic;
using UnityEngine;
using SpiritsCrossing.Autonomous;

namespace SpiritsCrossing.Cosmos
{
    public class RUEOrbitalRenderer : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("Orbital Scaling")]
        [Tooltip("Unity world units per RUE orbital radius unit.\n" +
                 "RUE planet radii are 5, 8, 11, 14, 17... (5 + index * 3).\n" +
                 "Set so the outermost planet fits inside your cosmos map bounds.")]
        public float unitsPerOrbitalUnit = 15f;

        [Tooltip("Y position for all planet nodes in the cosmos map scene.")]
        public float planetY = 0f;

        [Header("Update Rate")]
        [Tooltip("Seconds between position updates. 0.5s gives smooth visible orbit motion.")]
        [Range(0.1f, 5f)] public float orbitUpdateInterval = 0.5f;

        [Header("Star")]
        [Tooltip("Optional: the star GameObject. If assigned, it is kept at world origin.")]
        public GameObject starObject;

        [Header("Orbit Visual Rings")]
        [Tooltip("If true, draws orbit rings at each planet's orbital radius using LineRenderer.\n" +
                 "Requires a LineRenderer component on each child named 'Orbit_{planetId}'.")]
        public bool drawOrbitRings = true;
        [Range(16, 128)] public int orbitRingSegments = 64;

        [Header("Debug")]
        public bool logPositionUpdates;

        // -------------------------------------------------------------------------
        // Internal
        // -------------------------------------------------------------------------
        private float _timer;
        private readonly Dictionary<string, PlanetNodeController> _nodes
            = new Dictionary<string, PlanetNodeController>();

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void Start()
        {
            CacheNodeControllers();

            // Pin star at origin
            if (starObject != null)
                starObject.transform.position = new Vector3(0f, planetY, 0f);

            // Initial position update
            UpdateAllPositions();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < orbitUpdateInterval) return;
            _timer = 0f;
            UpdateAllPositions();
        }

        // -------------------------------------------------------------------------
        // Cache PlanetNodeControllers by planetId
        // -------------------------------------------------------------------------
        private void CacheNodeControllers()
        {
            _nodes.Clear();
            foreach (var ctrl in FindObjectsOfType<PlanetNodeController>())
                _nodes[ctrl.planetId] = ctrl;

            if (logPositionUpdates)
                Debug.Log($"[RUEOrbitalRenderer] Cached {_nodes.Count} planet nodes.");
        }

        // -------------------------------------------------------------------------
        // Core position update — position_from_polar() for every planet
        // -------------------------------------------------------------------------
        private void UpdateAllPositions()
        {
            var cosmosSystem   = CosmosGenerationSystem.Instance;
            var autonomySystem = PlanetAutonomySystem.Instance;

            if (cosmosSystem == null || !cosmosSystem.IsLoaded || autonomySystem == null)
                return;

            foreach (var planet in cosmosSystem.Data.planets)
            {
                if (!_nodes.TryGetValue(planet.planetId, out var node)) continue;

                float angle  = autonomySystem.GetOrbitalAngle(planet.planetId);
                float radius = planet.orbitalRadius * unitsPerOrbitalUnit;

                // RUE orbit.py: position_from_polar(r, θ) = (r*cos(θ), r*sin(θ))
                // Map to Unity XZ plane (Y is up)
                float x = radius * Mathf.Cos(angle);
                float z = radius * Mathf.Sin(angle);

                node.transform.position = new Vector3(x, planetY, z);
                node.Refresh();

                if (drawOrbitRings)
                    DrawOrbitRing(planet.planetId, radius);

                if (logPositionUpdates)
                    Debug.Log($"[RUEOrbitalRenderer] {planet.planetId}: " +
                              $"θ={angle:F3} r={radius:F1} → ({x:F1}, {z:F1})");
            }
        }

        // -------------------------------------------------------------------------
        // Orbit ring drawing — visual equivalent of plt.Circle in SimpleRenderer
        // -------------------------------------------------------------------------
        private readonly Dictionary<string, LineRenderer> _orbitLines
            = new Dictionary<string, LineRenderer>();

        private void DrawOrbitRing(string planetId, float radius)
        {
            if (!_orbitLines.TryGetValue(planetId, out var lr))
            {
                // Try to find a child with name "Orbit_{planetId}"
                string childName = $"Orbit_{planetId}";
                var existing = transform.Find(childName);
                if (existing == null)
                {
                    var go = new GameObject(childName);
                    go.transform.SetParent(transform, false);
                    lr = go.AddComponent<LineRenderer>();
                    lr.useWorldSpace = true;
                    lr.loop          = false; // we close the ring explicitly (last point == first)
                    lr.startWidth    = 0.2f;
                    lr.endWidth      = 0.2f;
                    lr.positionCount = orbitRingSegments + 1; // +1 closes back to start angle

                    // Default dim material — designer can override
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    mat.color = new Color(1f, 1f, 1f, 0.15f);
                    lr.material = mat;
                }
                else
                {
                    lr = existing.GetComponent<LineRenderer>();
                    if (lr == null) lr = existing.gameObject.AddComponent<LineRenderer>();
                }
                _orbitLines[planetId] = lr;
            }

            // Draw the orbit circle
            for (int i = 0; i <= orbitRingSegments; i++)
            {
                float angle = i / (float)orbitRingSegments * Mathf.PI * 2f;
                float x = radius * Mathf.Cos(angle);
                float z = radius * Mathf.Sin(angle);
                lr.SetPosition(i, new Vector3(x, planetY, z));
            }
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Force-refresh all planet positions immediately (e.g. on scene load).
        /// </summary>
        public void ForceRefresh()
        {
            CacheNodeControllers();
            UpdateAllPositions();
        }

        /// <summary>
        /// Get the current Unity world position for a planet by id.
        /// Returns Vector3.zero if the planet is not found.
        /// </summary>
        public Vector3 GetPlanetWorldPosition(string planetId)
        {
            if (_nodes.TryGetValue(planetId, out var node))
                return node.transform.position;
            return Vector3.zero;
        }
    }
}
