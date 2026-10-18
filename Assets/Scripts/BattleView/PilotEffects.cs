using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// The particle effects of one pilot slot. Each is an optional child particle system of the PilotSlot prefab (restyle
    /// them freely there); a missing one is skipped. All but Targeted play once; Targeted loops while an announced enemy
    /// effect is aimed at the pilot.
    /// </summary>
    public class PilotEffects : MonoBehaviour
    {
        [Tooltip("A pilot is placed in the slot.")]
        [SerializeField] private ParticleSystem _summon;

        [Tooltip("The pilot uses its ability.")]
        [SerializeField] private ParticleSystem _ability;

        [Tooltip("The pilot's chain takes a hit (before Defeated, when the hit makes it fall).")]
        [SerializeField] private ParticleSystem _hit;

        [Tooltip("The pilot falls.")]
        [SerializeField] private ParticleSystem _defeated;

        [Tooltip("One of the pilot's stats goes up (for example Surge).")]
        [SerializeField] private ParticleSystem _buff;

        [Tooltip("One of the pilot's stats goes down (an enemy snipe, Surge's crash).")]
        [SerializeField] private ParticleSystem _debuff;

        [Tooltip("Loops while an announced enemy effect is aimed at the pilot (a lock, a snipe...). Set its Looping on.")]
        [SerializeField] private ParticleSystem _targeted;

        public void PlaySummon() => Play(_summon);
        public void PlayAbility() => Play(_ability);
        public void PlayHit() => Play(_hit);
        public void PlayDefeated() => Play(_defeated);
        public void PlayBuff() => Play(_buff);
        public void PlayDebuff() => Play(_debuff);

        /// <summary>Starts or stops the Targeted loop (nothing happens if it is already in that state).</summary>
        public void SetTargeted(bool targeted)
        {
            // emitting, not playing: a stopped loop still counts as playing while its last particles fade
            if (_targeted == null || targeted == _targeted.isEmitting)
            {
                return;
            }

            if (targeted)
            {
                _targeted.Play(true);
            }
            else
            {
                _targeted.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>Stops everything at once (the slot is cleared or the battle restarts).</summary>
        public void StopAll()
        {
            foreach (var system in new[] { _summon, _ability, _hit, _defeated, _buff, _debuff, _targeted })
            {
                if (system != null)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private static void Play(ParticleSystem system)
        {
            if (system == null)
            {
                return;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(true);
        }
    }
}
