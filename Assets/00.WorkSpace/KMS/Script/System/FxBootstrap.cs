using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class FxBootstrap : MonoBehaviour
{
    [Header("Scene Refs")]
    [SerializeField] Canvas gameCanvas;
    [SerializeField] Canvas fxCanvas;
    [SerializeField] RawImage composite;
    [SerializeField] Camera fxCamera;

    [Header("Orders")]
    [SerializeField] int fxOrder = 32000;
    [SerializeField] int orderSafetyGap = 50;

    [Header("RT (optional asset)")]
    [SerializeField] RenderTexture rtAsset;
    [SerializeField] int fallbackWidth = 0;   // 0=Screen.width
    [SerializeField] int fallbackHeight = 0;  // 0=Screen.height

    void Awake()
    {
        // FXCanvas 최상단
        if (!fxCanvas) { Debug.LogError("[FX] fxCanvas missing"); return; }
        fxCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fxCanvas.overrideSorting = true;
        int baseOrder = (gameCanvas ? gameCanvas.sortingOrder + orderSafetyGap : 0);
        fxCanvas.sortingOrder = Mathf.Clamp(Mathf.Max(baseOrder, fxOrder), -32767, 32767);

        if (composite) composite.transform.SetAsLastSibling();

        // FXCamera 설정
        if (!fxCamera || !composite) { Debug.LogError("[FX] Assign fxCamera & composite RawImage."); return; }
        fxCamera.clearFlags = CameraClearFlags.Depth;
        // fxCamera.backgroundColor = new Color(0, 0, 0, 0);
        fxCamera.cullingMask = LayerMask.GetMask("FX");

        // RT 준비(자산 우선, 없으면 생성)
        var rt = rtAsset;
        if (!rt)
        {
            int w = fallbackWidth > 0 ? fallbackWidth : Screen.width;
            int h = fallbackHeight > 0 ? fallbackHeight : Screen.height;
            rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32)
            {
                useMipMap = false,
                autoGenerateMips = false,
                name = "FX_RT"
            };
            rt.Create();
        }

        fxCamera.targetTexture = rt;
        composite.texture = rt;
        composite.raycastTarget = false;

        Debug.Log($"[FX] Canvas order={fxCanvas.sortingOrder}, RT={rt.name} {rt.width}x{rt.height} fmt={rt.format}");
    }
}
