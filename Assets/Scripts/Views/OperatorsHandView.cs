using System.Collections.Generic;
using Configurations.Operators;
using UnityEngine;

namespace Views
{
    public class OperatorsHandView : MonoBehaviour
    {
        [SerializeField] private List<OperationTokenView> _tokenViews;
        
        public void InitOperatorTokenAtIndex(int index, OperatorTokenConfiguration configuration)
        {
            //TODO: DRY from pilot script. Try to generify
            var isIndexValid = _tokenViews.Count >= index;
            if (!isIndexValid)
            {
                Debug.LogError("OperatorsHandView - InitOperatorTokenAtIndex error: index is out of range!");
                return;
            }
            
            _tokenViews[index].Init(configuration);
        }
    }
}
