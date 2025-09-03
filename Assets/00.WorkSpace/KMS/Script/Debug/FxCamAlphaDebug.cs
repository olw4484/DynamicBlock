using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FxCamAlphaDebug : MonoBehaviour
{
    public Camera fxCamera;
    public KeyCode toggle = KeyCode.F6;
    void Update()
    {
        if (Input.GetKeyDown(toggle) && fxCamera)
        {
            var c = fxCamera.backgroundColor;
            c.a = c.a > 0.5f ? 0f : 1f;
            fxCamera.backgroundColor = c;
            Debug.Log($"FXCam alpha -> {c.a}");
        }
    }
}
