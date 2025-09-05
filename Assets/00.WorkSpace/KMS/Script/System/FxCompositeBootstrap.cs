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
    public int width = 0;              // 0�̸� Screen.width
    public int height = 0;             // 0�̸� Screen.height

    void Awake()
    {
        if (!fxCamera || !composite)
        {
            Debug.LogError("[FX] Assign fxCamera & composite RawImage.");
            return;
        }

        // 1) FXCamera �⺻�� ����
        fxCamera.clearFlags = CameraClearFlags.SolidColor;
        fxCamera.backgroundColor = new Color(0, 0, 0, 0);
        fxCamera.cullingMask = LayerMask.GetMask("FX");

        // 2) RT �غ� (������ ������ ����)
        var rt = rtAsset;
        if (!rt)
        {
            int w = width > 0 ? width : Screen.width;
            int h = height > 0 ? height : Screen.height;
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            rt.name = "FX_RT_Runtime";
            rt.Create();
        }

        // 3) �輱 ����: ���� �ν��Ͻ��� SET
        fxCamera.targetTexture = rt;
        composite.texture = rt;

        // 4) ���� �α�
        Debug.Log($"[FX] Wired: RawImage.texture == fxCamera.targetTexture ? " +
                  $"{(ReferenceEquals(composite.texture, fxCamera.targetTexture) ? "OK" : "MISMATCH")} " +
                  $"rt={rt.name} format={rt.format} size={rt.width}x{rt.height}");
    }
}
