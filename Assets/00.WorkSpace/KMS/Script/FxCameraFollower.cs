using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FxCameraFollower : MonoBehaviour
{
    public Camera mainCam;
    public Camera fxCam;

    void LateUpdate()
    {
        if (!mainCam || !fxCam) return;
        fxCam.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
        fxCam.orthographic = mainCam.orthographic;
        fxCam.fieldOfView = mainCam.fieldOfView;
        fxCam.orthographicSize = mainCam.orthographicSize;
        fxCam.nearClipPlane = mainCam.nearClipPlane;
        fxCam.farClipPlane = mainCam.farClipPlane;
    }
}
