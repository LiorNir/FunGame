using FunGame.Board;
using UnityEngine;

namespace BoardAuthoring
{
    /// <summary>
    /// Marks the slot it sits on as the terminal (last slot) of a chain. The chain is the path from a root to this
    /// slot, belongs to a lane, and reads either attack or defense. A slot may carry several markers.
    /// </summary>
    [RequireComponent(typeof(BoardSlotAuthoring))]
    public class BoardChainMarker : MonoBehaviour
    {
        [Tooltip("Free text, e.g. left or right. Must match a lane of the encounter.")]
        [SerializeField] private string _laneId = "lane1";

        [SerializeField] private ChainRole _role;

        public string LaneId => _laneId;
        public ChainRole Role => _role;
        public BoardSlotAuthoring Slot => GetComponent<BoardSlotAuthoring>();

        public void Configure(string laneId, ChainRole role)
        {
            _laneId = laneId;
            _role = role;
        }
    }
}
