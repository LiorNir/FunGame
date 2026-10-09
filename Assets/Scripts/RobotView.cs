using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RobotView : MonoBehaviour
{
    [SerializeField] private List<GeneralSlot> _pilotSlots; //Shouldn't be general
    [SerializeField] private List<OperatorSlot> _operatorSlots; //Make script that inherits from generalSlot

    [SerializeField] private float _attackValue;
    [SerializeField] private float _defenceValue;

    [SerializeField] private TextMeshProUGUI _finalAttackText;
    [SerializeField] private TextMeshProUGUI _finalDefenseText;

    public void CalculateValues()
    {
        CalculateAttackValue();
        //defense
    }
    
    public float CalculateAttackValue()
    {
        var firstPilot = _pilotSlots[0];
        var secondPilot = _pilotSlots[1];
        var thirdPilot = _pilotSlots[2];

        var firstOperator = _operatorSlots[0];
        var secondOperator = _operatorSlots[1];

        var firstCalculatedValue =
            firstOperator.CalculateValueBasedOnType(firstPilot.AttackValue, secondPilot.AttackValue);
        var finalValue = secondOperator.CalculateValueBasedOnType(firstCalculatedValue, thirdPilot.AttackValue);

        _finalAttackText.text = finalValue.ToString();
        return finalValue;
    }
    
    private float AddSlots(float firstSlotValue, float secondFloatValue)
    {
        return firstSlotValue + secondFloatValue;
    }
    
    private float SubtractSlots(float firstSlotValue, float secondFloatValue)
    {
        return firstSlotValue - secondFloatValue;
    }
    
    private float MultiplySlots(float firstSlotValue, float secondFloatValue)
    {
        return firstSlotValue * secondFloatValue;
    }
    
    private float DivideSlots(float firstSlotValue, float secondFloatValue)
    {
        return firstSlotValue / secondFloatValue;
    }
}
