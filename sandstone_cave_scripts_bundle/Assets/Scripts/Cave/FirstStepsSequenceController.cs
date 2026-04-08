using UnityEngine;
using Upsilon.Portals;

namespace Upsilon.Cave
{
    public class FirstStepsSequenceController : MonoBehaviour
    {
        public enum SequenceStage
        {
            Dormant,
            PlayerEntered,
            AnimalAwakened,
            SpiritAwakened,
            AIAwakened,
            PortalReady,
            Complete
        }

        [SerializeField] private CaveResonanceDisk resonanceDisk;
        [SerializeField] private SandstoneFigureSeed animalSeed;
        [SerializeField] private SandstoneFigureSeed aiSeed;
        [SerializeField] private GameObject animalSpiritObject;
        [SerializeField] private PortalStateController portal;
        [SerializeField] private Light caveMasterLight;
        [SerializeField] private AudioSource caveAudio;

        [SerializeField] private float playerEnteredDelay = 1f;
        [SerializeField] private float animalAwakenDelay = 2f;
        [SerializeField] private float spiritAwakenDelay = 3.5f;
        [SerializeField] private float aiAwakenDelay = 5f;

        public SequenceStage CurrentStage { get; private set; } = SequenceStage.Dormant;

        private float stageTimer;
        private bool entered;

        private void Start()
        {
            if (animalSpiritObject != null)
                animalSpiritObject.SetActive(false);
        }

        private void Update()
        {
            if (resonanceDisk == null) return;

            if (!entered && resonanceDisk.PlayerInside)
            {
                entered = true;
                stageTimer = 0f;
                CurrentStage = SequenceStage.PlayerEntered;
            }

            if (!entered) return;

            stageTimer += Time.deltaTime;

            if (CurrentStage == SequenceStage.PlayerEntered && stageTimer >= animalAwakenDelay)
            {
                animalSeed?.SpawnEntity();
                CurrentStage = SequenceStage.AnimalAwakened;
            }

            if (CurrentStage == SequenceStage.AnimalAwakened && stageTimer >= spiritAwakenDelay)
            {
                if (animalSpiritObject != null)
                    animalSpiritObject.SetActive(true);

                CurrentStage = SequenceStage.SpiritAwakened;
            }

            if (CurrentStage == SequenceStage.SpiritAwakened && stageTimer >= aiAwakenDelay)
            {
                aiSeed?.SpawnEntity();
                CurrentStage = SequenceStage.AIAwakened;
            }

            if (CurrentStage == SequenceStage.AIAwakened && portal != null && portal.IsStable)
            {
                CurrentStage = SequenceStage.PortalReady;
            }

            if (CurrentStage == SequenceStage.PortalReady)
            {
                ApplyCompleteLook();
                CurrentStage = SequenceStage.Complete;
            }

            ApplyAmbientLook();
        }

        private void ApplyAmbientLook()
        {
            if (caveMasterLight != null)
            {
                float target = 0.9f;

                switch (CurrentStage)
                {
                    case SequenceStage.PlayerEntered: target = 1.2f; break;
                    case SequenceStage.AnimalAwakened: target = 1.5f; break;
                    case SequenceStage.SpiritAwakened: target = 2.0f; break;
                    case SequenceStage.AIAwakened: target = 2.5f; break;
                    case SequenceStage.PortalReady: target = 3.2f; break;
                    case SequenceStage.Complete: target = 3.6f; break;
                }

                caveMasterLight.intensity = Mathf.Lerp(caveMasterLight.intensity, target, Time.deltaTime * 1.5f);
            }

            if (caveAudio != null)
            {
                float targetPitch = 0.9f + ((int)CurrentStage * 0.05f);
                caveAudio.pitch = Mathf.Lerp(caveAudio.pitch, targetPitch, Time.deltaTime);
            }
        }

        private void ApplyCompleteLook()
        {
            if (caveMasterLight != null)
                caveMasterLight.color = new Color(0.7f, 0.85f, 1f);
        }
    }
}
