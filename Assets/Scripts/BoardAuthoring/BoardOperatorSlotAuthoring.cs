using System.Collections.Generic;
using FunGame.Board;
using UnityEngine;

namespace BoardAuthoring
{
    /// <summary>
    /// An operator slot in a board prefab. It takes the value from one pilot slot (<see cref="From"/>, nearer the
    /// player) and passes it on to one or more pilot slots (<see cref="Outputs"/>). Each output pilot slot receives
    /// the value combined with its own pilot's stat using this slot's operator.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardOperatorSlotAuthoring : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Tooltip("Unique within the board.")]
        [SerializeField] private string _id;

        [Tooltip("The operator applied to the value flowing through this slot: value OP the next pilot's stat.")]
        [SerializeField] private Operation _operation = Operation.Add;

        [Tooltip("The pilot slot the value comes from (nearer the player).")]
        [SerializeField] private BoardSlotAuthoring _from;

        [Tooltip("The pilot slots the value flows into. More than one makes the chain branch here.")]
        [SerializeField] private List<BoardSlotAuthoring> _outputs = new List<BoardSlotAuthoring>();

        // Boards made before an operator slot could have several outputs stored a single "To". It is moved into
        // the outputs list when the object loads.
        [SerializeField, HideInInspector] private BoardSlotAuthoring _to;

        public string Id => _id;
        public Operation Operation => _operation;
        public BoardSlotAuthoring From => _from;
        public IReadOnlyList<BoardSlotAuthoring> Outputs => _outputs;

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

        public void Configure(string id, BoardSlotAuthoring from, BoardSlotAuthoring to, Operation operation = Operation.Add)
        {
            _id = id;
            _from = from;
            _outputs = new List<BoardSlotAuthoring>();
            if (to != null)
            {
                _outputs.Add(to);
            }

            _operation = operation;
        }

        public void AddOutput(BoardSlotAuthoring output)
        {
            if (output != null && !_outputs.Contains(output))
            {
                _outputs.Add(output);
            }
        }

        public bool HasOutput(BoardSlotAuthoring slot)
        {
            return _outputs.Contains(slot);
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (_to == null)
            {
                return;
            }

            if (_outputs == null)
            {
                _outputs = new List<BoardSlotAuthoring>();
            }

            if (!_outputs.Contains(_to))
            {
                _outputs.Insert(0, _to);
            }

            _to = null;
        }
    }
}
