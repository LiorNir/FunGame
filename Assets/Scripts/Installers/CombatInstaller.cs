using UnityEngine;
using Views;
using Zenject;

namespace Installers
{
    public class CombatInstaller : MonoInstaller<CombatInstaller>
    {
        [SerializeField] private PilotHandView _pilotHandView;
        
        public override void InstallBindings()
        {
            Container.BindInstance(_pilotHandView).AsSingle().NonLazy();
        }
    }
}
