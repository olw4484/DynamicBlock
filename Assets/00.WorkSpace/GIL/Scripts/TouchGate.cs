using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TouchGate
{
    public static int TouchId = int.MinValue;

    public static void SetTouchID(int value)
    {
        TouchId = value;
    }
    
    public static int GetTouchID()
    {
        return TouchId;
    }
}
