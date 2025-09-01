using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TouchGate
{
    public static int TouchId = int.MinValue;

    public static void SetTouchID(int value)
    {
        TouchId = value;
        Debug.Log("Set Touch ID: " + TouchId);
    }
    
    public static int GetTouchID()
    {
        Debug.Log("Get Touch ID: " + TouchId);
        return TouchId;
    }
}
