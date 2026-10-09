using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OperatorSlot : GeneralSlot
{
   [SerializeField] private OperationType _operationType;

   public OperationType OperationType => _operationType;

   public float CalculateValueBasedOnType(float firstValue, float secondValue)
   {
      switch (OperationType)
      {
         case OperationType.Add:
            return firstValue + secondValue;
         case OperationType.Subtract:
            return firstValue + secondValue;
         case OperationType.Multiply:
            return firstValue + secondValue;
         case OperationType.Divide:
            return firstValue + secondValue;
      }

      return 0f;
   }
}
