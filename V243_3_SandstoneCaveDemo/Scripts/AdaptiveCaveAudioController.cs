using UnityEngine;

namespace V243.SandstoneCave
{
    public class AdaptiveCaveAudioController : MonoBehaviour
    {
        public enum AudioMode
        {
            Idle,
            Seated,
            Flow,
            Dervish,
            Pair,
            CrownHold,
            PlanetReveal
        }

        [Header("Sources")]
        public AudioSource idleDrone;
        public AudioSource seatedLayer;
        public AudioSource flowLayer;
        public AudioSource dervishLayer;
        public AudioSource pairLayer;
        public AudioSource crownLayer;
        public AudioSource revealLayer;

        [Header("References")]
        public PlayerResponseTracker playerResponseTracker;
        public CaveSessionController sessionController;
        public float crossfadeSpeed = 1.5f;

        [Header("Runtime")]
        public AudioMode currentMode = AudioMode.Idle;

        private void Update()
        {
            AudioMode target = ResolveMode();
            currentMode = target;
            ApplyMix(target);
        }

        public void ForcePlanetReveal()
        {
            currentMode = AudioMode.PlanetReveal;
            ApplyMix(currentMode);
        }

        private AudioMode ResolveMode()
        {
            if (sessionController != null && sessionController.chakraState.isHolding)
            {
                return AudioMode.CrownHold;
            }

            if (playerResponseTracker == null)
            {
                return AudioMode.Idle;
            }

            CavePlayerResonanceState s = playerResponseTracker.LiveState;
            if (s.socialSync >= 0.65f)
            {
                return AudioMode.Pair;
            }
            if (s.spinStability >= 0.65f)
            {
                return AudioMode.Dervish;
            }
            if (s.movementFlow >= 0.6f)
            {
                return AudioMode.Flow;
            }
            if (s.breathCoherence >= 0.55f && s.calm >= 0.55f)
            {
                return AudioMode.Seated;
            }

            return AudioMode.Idle;
        }

        private void ApplyMix(AudioMode mode)
        {
            FadeTo(idleDrone, mode == AudioMode.Idle ? 0.65f : 0.35f);
            FadeTo(seatedLayer, mode == AudioMode.Seated ? 1f : 0f);
            FadeTo(flowLayer, mode == AudioMode.Flow ? 1f : 0f);
            FadeTo(dervishLayer, mode == AudioMode.Dervish ? 1f : 0f);
            FadeTo(pairLayer, mode == AudioMode.Pair ? 1f : 0f);
            FadeTo(crownLayer, mode == AudioMode.CrownHold ? 1f : 0f);
            FadeTo(revealLayer, mode == AudioMode.PlanetReveal ? 1f : 0f);
        }

        private void FadeTo(AudioSource source, float targetVolume)
        {
            if (source == null)
            {
                return;
            }

            if (!source.isPlaying)
            {
                source.Play();
            }

            source.volume = Mathf.MoveTowards(source.volume, targetVolume, Time.deltaTime * crossfadeSpeed);
        }
    }
}
