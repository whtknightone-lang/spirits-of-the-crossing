using System.Collections.Generic;
using UnityEngine;

namespace V243.SandstoneCave
{
    public class CenterDiskPerformanceController : MonoBehaviour
    {
        public Transform centerDiskTarget;
        public List<SpiritLikenessController> spirits = new List<SpiritLikenessController>();
        public PlayerResponseTracker playerResponseTracker;

        [Header("Selection Thresholds")]
        public float seatedThreshold = 0.55f;
        public float flowThreshold = 0.55f;
        public float spinThreshold = 0.55f;
        public float pairThreshold = 0.55f;

        private void Start()
        {
            foreach (SpiritLikenessController spirit in spirits)
            {
                if (spirit == null) continue;
                spirit.centerDiskTarget = centerDiskTarget;
                spirit.responseTracker = playerResponseTracker;
            }
        }

        private void Update()
        {
            if (playerResponseTracker == null)
            {
                return;
            }

            CavePlayerResonanceState state = playerResponseTracker.LiveState;
            foreach (SpiritLikenessController spirit in spirits)
            {
                if (spirit == null || spirit.awakened) continue;

                switch (spirit.archetype)
                {
                    case SpiritArchetype.Seated when (state.breathCoherence * state.calm) >= seatedThreshold:
                    case SpiritArchetype.FlowDancer when state.movementFlow >= flowThreshold:
                    case SpiritArchetype.Dervish when state.spinStability >= spinThreshold:
                    case SpiritArchetype.PairA when state.socialSync >= pairThreshold:
                    case SpiritArchetype.PairB when state.socialSync >= pairThreshold:
                        spirit.awakened = true;
                        break;
                }
            }
        }
    }
}
