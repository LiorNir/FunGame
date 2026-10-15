using System;
using FunGame.Battle;
using UnityEngine;

namespace Configurations
{
    [CreateAssetMenu(fileName = "PilotTokenConfiguration", menuName = "ScriptableObjects/Tokens/PilotTokenConfiguration")]
    [Serializable]
    public class PilotTokenConfiguration : ScriptableObject
    {
        [SerializeField] public string Name;
        [SerializeField] public string DisplayName;
        [SerializeField] public Sprite Sprite;
        [SerializeField] public float AttackValue;
        [SerializeField] public float DefenseValue;
        [SerializeField] public bool Ability; //TODO: temp bool just for indication

        [Header("Ability")]
        [Tooltip("The pilot's ability (used once per encounter, from the board).")]
        [SerializeField] public PilotAbilityKind AbilityKind;

        [Tooltip("What the ability costs in AP. Every ability has its own cost.")]
        [Min(0)] [SerializeField] public int AbilityApCost = 2;

        [Tooltip("Stable id for save files (the asset's GUID), kept up to date by the editor. Do not edit.")]
        [HideInInspector] [SerializeField] private string _assetId;

        /// <summary>The id save files use for this pilot; it survives renaming the asset (see <see cref="AssetIds"/>).</summary>
        public string AssetId => AssetIds.Of(this, _assetId);

#if UNITY_EDITOR
        private void OnValidate()
        {
            AssetIds.Stamp(this, ref _assetId);
        }
#endif
    }
}
