using FunGame.Board;
using UnityEngine;

namespace BoardAuthoring
{
    /// <summary>
    /// One pilot slot in a board prefab. Its position is simply where the GameObject sits. A slot with no incoming
    /// operator slot is a root (nearest the player); otherwise values reach it through that operator slot. Everything
    /// about the slot is defined here: its bonus and how it unlocks.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardSlotAuthoring : MonoBehaviour
    {
        [Tooltip("Unique within the board.")]
        [SerializeField] private string _id;

        [Header("Bonus")]
        [Tooltip("An extra effect applied to the accumulated value after this slot's pilot, when the slot is filled.")]
        [SerializeField] private BonusKind _bonusKind = BonusKind.None;
        [SerializeField] private float _bonusAmount;

        [Header("Locking")]
        [Tooltip("The slot starts the battle locked and cannot hold a pilot.")]
        [SerializeField] private bool _locked;

        [Tooltip("AP the player can pay to unlock the slot. 0 means it cannot be bought.")]
        [Min(0)] [SerializeField] private int _unlockApCost;

        [Tooltip("Unlocks when this enemy of the chosen lane appears (2 = the second enemy). 0 means never by wave.")]
        [Min(0)] [SerializeField] private int _unlockAtWave;

        [Tooltip("Unlocks at the start of this turn of the battle (2 = the second turn). 0 means never by turn.")]
        [Min(0)] [SerializeField] private int _unlockAtTurn;

        [Tooltip("For a wave unlock: the lane whose enemy number counts. Wave 2 here means the second enemy of this lane has appeared. May stay empty only on a board with a single lane.")]
        [LaneId] [SerializeField] private string _unlockLaneId;

        public string Id => _id;
        public string UnlockLaneId => _unlockLaneId;
        public NodeBonus Bonus => new NodeBonus(_bonusKind, _bonusAmount);
        public BonusKind BonusKind => _bonusKind;
        public float BonusAmount => _bonusAmount;
        public bool IsLocked => _locked;
        public int UnlockApCost => _unlockApCost;
        public int UnlockAtWave => _unlockAtWave;
        public int UnlockAtTurn => _unlockAtTurn;

        /// <summary>True if a locked slot has some way of becoming unlocked.</summary>
        public bool HasUnlockPath => _unlockApCost > 0 || _unlockAtWave > 0 || _unlockAtTurn > 0;

        /// <summary>Radius of the slot in its own units, read from the playable look on this prefab.</summary>
        public float Radius
        {
            get
            {
                var footprint = GetComponent<ISlotFootprint>();
                return footprint != null ? footprint.Radius : SlotFootprint.FallbackRadius;
            }
        }

        /// <summary>Radius in world units, including the scale of the slot and the board it sits in.</summary>
        public float WorldRadius => Radius * Mathf.Abs(transform.lossyScale.x);

        public void Configure(string id)
        {
            _id = id;
        }

        public void ConfigureBonus(BonusKind kind, float amount)
        {
            _bonusKind = kind;
            _bonusAmount = amount;
        }

        public void ConfigureLock(bool locked, int unlockApCost, int unlockAtWave, int unlockAtTurn, string unlockLaneId = null)
        {
            _unlockLaneId = unlockLaneId;
            _locked = locked;
            _unlockApCost = unlockApCost;
            _unlockAtWave = unlockAtWave;
            _unlockAtTurn = unlockAtTurn;
        }
    }
}
