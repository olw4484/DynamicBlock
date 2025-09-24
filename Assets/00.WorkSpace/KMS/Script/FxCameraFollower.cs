using UnityEngine;

[DefaultExecutionOrder(1000)]
public class FxCameraFollower : MonoBehaviour
{
    public Camera mainCam;
    public Camera fxCam;

    void Awake()
    {
        if (!mainCam) mainCam = Camera.main;
        if (!fxCam) fxCam = GetComponent<Camera>();
        if (!mainCam || !fxCam) return;

        fxCam.cullingMask = LayerMask.GetMask("FX"); // FX 레이어만
        fxCam.depth = mainCam.depth + 1;
        fxCam.clearFlags = CameraClearFlags.Depth;
        fxCam.targetTexture = null;
    }

    void LateUpdate()
    {
        if (!mainCam || !fxCam) return;

        fxCam.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
        fxCam.rect = mainCam.rect;
        fxCam.projectionMatrix = mainCam.projectionMatrix;
        fxCam.orthographic = mainCam.orthographic;
        fxCam.fieldOfView = mainCam.fieldOfView;
        fxCam.orthographicSize = mainCam.orthographicSize;
        fxCam.nearClipPlane = mainCam.nearClipPlane;
        fxCam.farClipPlane = mainCam.farClipPlane;

        fxCam.allowHDR = mainCam.allowHDR;
        fxCam.allowMSAA = mainCam.allowMSAA;
    }
}
