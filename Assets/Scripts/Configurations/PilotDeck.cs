using System.Collections.Generic;
using UnityEngine;

namespace Configurations
{
    /// <summary>The pilots the player brings into a battle. A pilot may be listed more than once.</summary>
    [CreateAssetMenu(fileName = "PilotDeck", menuName = "ScriptableObjects/PilotDeck")]
    public class PilotDeck : ScriptableObject
    {
        [SerializeField] private List<PilotTokenConfiguration> _pilots = new List<PilotTokenConfiguration>();

        public IReadOnlyList<PilotTokenConfiguration> Pilots => _pilots;

        public void SetData(List<PilotTokenConfiguration> pilots)
        {
            _pilots = pilots;
        }
    }
}
