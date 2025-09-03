using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FxCompositeBootstrap : MonoBehaviour
{
    [Header("Refs")]
    public Camera fxCamera;
    public RawImage composite;         // RawImage(FXComposite)
    public RenderTexture rtAsset;      

    [Header("Create RT if null")]
    public int width = 0;              // 0이면 Screen.width
    public int height = 0;             // 0이면 Screen.height

    void Awake()
    {
        if (!fxCamera || !composite)
        {
            Debug.LogError("[FX] Assign fxCamera & composite RawImage.");
            return;
        }

        // 1) FXCamera 기본값 강제
        fxCamera.clearFlags = CameraClearFlags.SolidColor;
        fxCamera.backgroundColor = new Color(0, 0, 0, 0);
        fxCamera.cullingMask = LayerMask.GetMask("FX");

        // 2) RT 준비 (에셋이 없으면 생성)
        var rt = rtAsset;
        if (!rt)
        {
            int w = width > 0 ? width : Screen.width;
            int h = height > 0 ? height : Screen.height;
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            rt.name = "FX_RT_Runtime";
            rt.Create();
        }

        // 3) 배선 통일: 동일 인스턴스로 SET
        fxCamera.targetTexture = rt;
        composite.texture = rt;

        // 4) 진단 로그
        Debug.Log($"[FX] Wired: RawImage.texture == fxCamera.targetTexture ? " +
                  $"{(ReferenceEquals(composite.texture, fxCamera.targetTexture) ? "OK" : "MISMATCH")} " +
                  $"rt={rt.name} format={rt.format} size={rt.width}x{rt.height}");
    }
}
