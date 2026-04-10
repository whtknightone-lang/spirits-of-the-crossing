// SpiritsCrossing — UpsilonSphereBootstrapTests.cs
// Play Mode unit tests for UpsilonSphereBootstrap self-assembly.
//
// Run via: Window > General > Test Runner > PlayMode > UpsilonSphereBootstrapTests
//
// Each test creates a fresh GameObject, activates the bootstrap, waits one
// frame for Awake() to fire, then asserts the expected outcome.
//
// Tests that need to configure bootstrap BEFORE Awake runs use the
// SetActive(false) → AddComponent → configure → SetActive(true) pattern,
// which is the standard Unity approach for pre-configuring MonoBehaviours.

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UpsilonPath.Sphere;

namespace SpiritsCrossing.Tests
{
    public class UpsilonSphereBootstrapTests
    {
        private GameObject _go;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BootstrapTestObject");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.Destroy(_go);
        }

        // -----------------------------------------------------------------------
        // 1. All three components are added by default
        // -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bootstrap_AddsAllThreeComponents()
        {
            _go.AddComponent<UpsilonSphereBootstrap>();
            yield return null; // allow Awake to fire

            Assert.IsNotNull(_go.GetComponent<UpsilonSphere>(),
                "UpsilonSphere should be added automatically by Bootstrap");
            Assert.IsNotNull(_go.GetComponent<UpsilonResonanceMemory>(),
                "UpsilonResonanceMemory should be added automatically by Bootstrap");
            Assert.IsNotNull(_go.GetComponent<UpsilonPerceptionBridge>(),
                "UpsilonPerceptionBridge should be added automatically by Bootstrap");
        }

        // -----------------------------------------------------------------------
        // 2. Cross-component references are wired to the correct instances
        // -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bootstrap_WiresReferencesCorrectly()
        {
            _go.AddComponent<UpsilonSphereBootstrap>();
            yield return null;

            var sphere = _go.GetComponent<UpsilonSphere>();
            var memory = _go.GetComponent<UpsilonResonanceMemory>();
            var bridge = _go.GetComponent<UpsilonPerceptionBridge>();

            Assert.AreSame(sphere, memory.sphere,
                "memory.sphere must point to the UpsilonSphere on the same GameObject");
            Assert.AreSame(sphere, bridge.sphere,
                "bridge.sphere must point to the UpsilonSphere on the same GameObject");
            Assert.AreSame(memory, bridge.resonanceMemory,
                "bridge.resonanceMemory must point to the UpsilonResonanceMemory on the same GameObject");
        }

        // -----------------------------------------------------------------------
        // 3. Default node count: one node per UpsilonQuestionNode enum value (7)
        // -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bootstrap_InitializesSevenDefaultNodes()
        {
            _go.AddComponent<UpsilonSphereBootstrap>();
            yield return null;

            var sphere = _go.GetComponent<UpsilonSphere>();
            Assert.AreEqual(7, sphere.nodes.Count,
                "UpsilonSphere should contain exactly 7 nodes after default initialization");
        }

        // -----------------------------------------------------------------------
        // 4. Every UpsilonQuestionNode enum value appears exactly once
        // -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bootstrap_NodeTypesAreAllDistinct()
        {
            _go.AddComponent<UpsilonSphereBootstrap>();
            yield return null;

            var sphere = _go.GetComponent<UpsilonSphere>();
            var seen = new HashSet<UpsilonQuestionNode>();

            foreach (var node in sphere.nodes)
            {
                bool isNew = seen.Add(node.nodeType);
                Assert.IsTrue(isNew,
                    $"Duplicate node type detected: {node.nodeType}. Each enum value must appear exactly once.");
            }

            Assert.AreEqual(7, seen.Count,
                "All 7 UpsilonQuestionNode values must be represented");
        }

        // -----------------------------------------------------------------------
        // 5. Default nodes carry valid (positive) frequency and coherence values
        // -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bootstrap_DefaultNodesHavePositiveFrequencyAndCoherence()
        {
            _go.AddComponent<UpsilonSphereBootstrap>();
            yield return null;

            var sphere = _go.GetComponent<UpsilonSphere>();
            foreach (var node in sphere.nodes)
            {
                Assert.Greater(node.band.baseFrequency, 0f,
                    $"Node {node.nodeType}: baseFrequency must be > 0");
                Assert.Greater(node.band.coherence, 0f,
                    $"Node {node.nodeType}: coherence must be > 0");
                Assert.GreaterOrEqual(node.activation, 0f,
                    $"Node {node.nodeType}: activation must be >= 0");
                Assert.LessOrEqual(node.activation, 1f,
                    $"Node {node.nodeType}: activation must be <= 1");
            }
        }

        // -----------------------------------------------------------------------
        // 6. autoCreateOnThisObject = false: no components are added
        // -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bootstrap_AutoCreateDisabled_AddsNoComponents()
        {
            // Disable the GameObject before adding the component so we can
            // configure it before Awake fires.
            _go.SetActive(false);
            var bootstrap = _go.AddComponent<UpsilonSphereBootstrap>();
            bootstrap.autoCreateOnThisObject = false;
            _go.SetActive(true);
            yield return null;

            Assert.IsNull(_go.GetComponent<UpsilonSphere>(),
                "UpsilonSphere must NOT be added when autoCreateOnThisObject=false");
            Assert.IsNull(_go.GetComponent<UpsilonResonanceMemory>(),
                "UpsilonResonanceMemory must NOT be added when autoCreateOnThisObject=false");
            Assert.IsNull(_go.GetComponent<UpsilonPerceptionBridge>(),
                "UpsilonPerceptionBridge must NOT be added when autoCreateOnThisObject=false");
        }

        // -----------------------------------------------------------------------
        // 7. initializeDefaultNodes = false: sphere is assembled but nodes list is empty
        // -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bootstrap_InitNodeDisabled_LeavesNodesEmpty()
        {
            _go.SetActive(false);
            var bootstrap = _go.AddComponent<UpsilonSphereBootstrap>();
            bootstrap.initializeDefaultNodes = false;
            _go.SetActive(true);
            yield return null;

            var sphere = _go.GetComponent<UpsilonSphere>();
            Assert.IsNotNull(sphere,
                "UpsilonSphere should still be added even with initializeDefaultNodes=false");
            Assert.AreEqual(0, sphere.nodes.Count,
                "Nodes list should remain empty when initializeDefaultNodes=false");
        }

        // -----------------------------------------------------------------------
        // 8. Idempotency: pre-existing components are reused, not duplicated
        // -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bootstrap_ExistingComponents_AreReusedNotDuplicated()
        {
            _go.SetActive(false);
            var existingSphere = _go.AddComponent<UpsilonSphere>();
            var existingMemory = _go.AddComponent<UpsilonResonanceMemory>();
            _go.AddComponent<UpsilonSphereBootstrap>();
            _go.SetActive(true);
            yield return null;

            Assert.AreEqual(1, _go.GetComponents<UpsilonSphere>().Length,
                "GetOrAdd must not create a second UpsilonSphere when one already exists");
            Assert.AreEqual(1, _go.GetComponents<UpsilonResonanceMemory>().Length,
                "GetOrAdd must not create a second UpsilonResonanceMemory when one already exists");
            Assert.AreSame(existingSphere, _go.GetComponent<UpsilonSphere>(),
                "Bootstrap should reference the pre-existing UpsilonSphere, not a new one");
            Assert.AreSame(existingMemory, _go.GetComponent<UpsilonResonanceMemory>(),
                "Bootstrap should reference the pre-existing UpsilonResonanceMemory, not a new one");
        }

        // -----------------------------------------------------------------------
        // 9. Optional perceptionLoopTarget is forwarded to the bridge
        // -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bootstrap_PerceptionTargetIsForwardedToBridge()
        {
            var targetGo = new GameObject("FakePerceptionTarget");
            var fakeTarget = targetGo.AddComponent<FakePerceptionLoop>();

            _go.SetActive(false);
            var bootstrap = _go.AddComponent<UpsilonSphereBootstrap>();
            bootstrap.perceptionLoopTarget = fakeTarget;
            _go.SetActive(true);
            yield return null;

            var bridge = _go.GetComponent<UpsilonPerceptionBridge>();
            Assert.AreSame(fakeTarget, bridge.targetPerceptionLoop,
                "bridge.targetPerceptionLoop must be the MonoBehaviour assigned to Bootstrap.perceptionLoopTarget");

            Object.Destroy(targetGo);
        }

        // -----------------------------------------------------------------------
        // 10. Sphere coherence values update after bootstrap (sanity smoke test)
        // -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bootstrap_SphereRecomputesCoherenceAfterInit()
        {
            _go.AddComponent<UpsilonSphereBootstrap>();
            yield return null; // Awake + first Update tick
            yield return null; // second Update tick to let RecomputeSphere run

            var sphere = _go.GetComponent<UpsilonSphere>();
            // overallCoherence should be non-zero once nodes are live and Update has run
            Assert.AreNotEqual(0f, sphere.overallCoherence,
                "overallCoherence should be non-zero after at least one RecomputeSphere pass");
        }

        // -----------------------------------------------------------------------
        // Minimal MonoBehaviour stub used as perceptionLoopTarget in test 9.
        // Defined as a private nested class to keep it contained.
        // -----------------------------------------------------------------------

        private class FakePerceptionLoop : MonoBehaviour { }
    }
}
