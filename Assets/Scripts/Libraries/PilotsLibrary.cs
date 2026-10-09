using System.Collections.Generic;
using System.Linq;
using Configurations;
using UnityEngine;

namespace Libraries
{
    [CreateAssetMenu(fileName = "PilotsLibrary", menuName = "ScriptableObjects/Libraries/PilotsLibrary")]
    public class PilotsLibrary : ScriptableObject
    {
        [SerializeField] private List<PilotTokenConfiguration> _pilotConfigurations;

        public PilotTokenConfiguration GetPilotConfigurationFromName(string name)
        {
            var configuration = _pilotConfigurations.FirstOrDefault(pilot => pilot.Name == name);
            if (configuration == null)
            {
                Debug.LogError(
                    $"PilotsLibrary - GetPilotConfigurationFromName error: Can't find pilot configuration for name {name}");
                return null;
            }

            return configuration;
        }

        public PilotTokenConfiguration GetRandomPilot() //TODO: for test, probably uneeded
        {
            var index = Random.Range(0, _pilotConfigurations.Count);
            return _pilotConfigurations[index];
        }
    }
}
