using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Lean.Touch;
using UnityEngine;

public interface IDraggableObject
{
    void OnClick(LeanFinger leanFinger);
    void OnRelease(LeanFinger leanFinger);
}
