using UnityEngine;

namespace BattleView
{
    /// <summary>
    /// The particle effects of one pilot slot. Each is an optional child particle system of the PilotSlot prefab (restyle
    /// them freely there); a missing one is skipped. All but the two loops play once; Targeted loops while an announced enemy
    /// effect is aimed at the pilot, Ability Ready while its ability can be used now (unused, and enough AP).
    /// </summary>
    public class PilotEffects : MonoBehaviour
    {
        [Tooltip("A pilot is placed in the slot.")]
        [SerializeField] private ParticleSystem _summon;

        [Tooltip("The pilot uses its ability.")]
        [SerializeField] private ParticleSystem _ability;

        [Tooltip("The pilot's chain takes a hit (before Defeated, when the hit makes it fall).")]
        [SerializeField] private ParticleSystem _hit;

        [Tooltip("Optional: a legacy Animation whose clip plays with Hit (the slot shakes). Edit its clip in the Animation window with the PilotSlot prefab open.")]
        [SerializeField] private Animation _hitAnimation;

        [Tooltip("The pilot falls.")]
        [SerializeField] private ParticleSystem _defeated;

        [Tooltip("One of the pilot's stats goes up (for example Surge).")]
        [SerializeField] private ParticleSystem _buff;

        [Tooltip("One of the pilot's stats goes down (an enemy snipe, Surge's crash).")]
        [SerializeField] private ParticleSystem _debuff;

        [Tooltip("Loops while an announced enemy effect is aimed at the pilot (a lock, a snipe...). Set its Looping on.")]
        [SerializeField] private ParticleSystem _targeted;

        [Tooltip("Loops while the pilot's ability can be used now: not used yet and enough AP. Set its Looping on.")]
        [SerializeField] private ParticleSystem _abilityReady;

        [Tooltip("On: Summon's particles take the slot's role colour (attack red, defense blue, both purple), replacing their Start Color. Off: Summon keeps the colours set on it.")]
        [SerializeField] private bool _tintSummonByRole = true;

        /// <summary>Plays Summon, in the slot's role colour unless <see cref="_tintSummonByRole"/> is off.</summary>
        public void PlaySummon(Color roleColor)
        {
            if (_summon != null && _tintSummonByRole)
            {
                var main = _summon.main;
                main.startColor = roleColor;
            }

            Play(_summon);
        }
        public void PlayAbility() => Play(_ability);
        public void PlayHit()
        {
            Play(_hit);
            if (_hitAnimation != null && _hitAnimation.clip != null)
            {
                _hitAnimation.Stop();
                _hitAnimation.Play(_hitAnimation.clip.name);
            }
        }

        public void PlayDefeated() => Play(_defeated);
        public void PlayBuff() => Play(_buff);
        public void PlayDebuff() => Play(_debuff);

        /// <summary>Starts or stops the Targeted loop (nothing happens if it is already in that state).</summary>
        public void SetTargeted(bool targeted) => SetLoop(_targeted, targeted);

        /// <summary>Starts or stops the Ability Ready loop (the ability can be used now).</summary>
        public void SetAbilityReady(bool ready) => SetLoop(_abilityReady, ready);

        private static void SetLoop(ParticleSystem system, bool on)
        {
            // emitting, not playing: a stopped loop still counts as playing while its last particles fade
            if (system == null || on == system.isEmitting)
            {
                return;
            }

            if (on)
            {
                system.Play(true);
            }
            else
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>Stops everything at once (the slot is cleared or the battle restarts).</summary>
        public void StopAll()
        {
            foreach (var system in new[] { _summon, _ability, _hit, _defeated, _buff, _debuff, _targeted, _abilityReady })
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
