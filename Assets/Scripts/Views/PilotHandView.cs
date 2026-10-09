using System.Collections.Generic;
using System.Linq;
using Configurations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Views
{
    public class PilotHandView : MonoBehaviour
    {
        [SerializeField] private List<PilotTokenView> _tokenViews; //TODO: populate from a pool or something
        [SerializeField] private float _xOffsetBetweenTokens = -2.8f; //TODO: to const?
        
        private List<PilotTokenView> _inactiveTokenViews = new List<PilotTokenView>();

        public void InitPilotTokenAtIndex(int index, PilotTokenConfiguration configuration)
        {
            var isIndexValid = _tokenViews.Count > index;
            if (!isIndexValid)
            {
                Debug.LogError("PilotHandView - InitPilotTokenAtIndex error: index is out of range!");
                return;
            }
            
            _tokenViews[index].Init(configuration, OnPilotTokenSuccessfullyPlaced);
        }

        public async UniTask SpawnNewToken(PilotTokenConfiguration configuration)
        {
            var token = _inactiveTokenViews.FirstOrDefault(); //TODO: create/get one from pool if list in empty
            _inactiveTokenViews.Remove(token);
            _tokenViews.Add(token);
            RearrangeTokens();
            token.gameObject.SetActive(true); //TODO: animate
        }

        private void OnPilotTokenSuccessfullyPlaced(PilotTokenView tokenView)
        {
            _tokenViews.Remove(tokenView);
            _inactiveTokenViews.Add(tokenView);
            RearrangeTokens();
        }

        private void RearrangeTokens()
        {
            var tokenAmount = _tokenViews.Count;
            var isTokenAmountEven = tokenAmount % 2 == 0; //TODO: can be less heavy maybe?
            var xOffset = isTokenAmountEven
                ? _xOffsetBetweenTokens - (_xOffsetBetweenTokens * 0.5f)
                : _xOffsetBetweenTokens;
            var firstTokenOffset = Mathf.Floor(tokenAmount * 0.5f) * _xOffsetBetweenTokens * -1f;
            var currentOffset = firstTokenOffset;
            foreach (var token in _tokenViews)
            {
                token.RepositionInHand(currentOffset).Forget();
                currentOffset += _xOffsetBetweenTokens;
            }
        }

    }
}
