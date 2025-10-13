using TMPro;
using UnityEngine;

public static class TMPOutlineUtil
{
    public static void ApplyOutlineToAllMaterials(TMP_Text tmp, float outlineWidth, Color outlineColor)
    {
        if (!tmp) return;

        // 1) 메인 머티리얼(인스턴스)
        SetOutlineSafe(tmp.fontMaterial, outlineWidth, outlineColor);

        // 2) UGUI용 서브메시(폴백 등)
        var subsUI = tmp.GetComponentsInChildren<TMP_SubMeshUI>(true);
        foreach (var sm in subsUI) SetOutlineSafe(sm.material, outlineWidth, outlineColor);

        // 3) 3D용 서브메시(텍스트 MeshPro 컴포넌트일 경우 대비)
        var subs = tmp.GetComponentsInChildren<TMP_SubMesh>(true);
        foreach (var sm in subs) SetOutlineSafe(sm.material, outlineWidth, outlineColor);

        // 4) 렌더 갱신
        tmp.extraPadding = true;
        tmp.havePropertiesChanged = true;
        tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
    }

    static void SetOutlineSafe(Material m, float width, Color color)
    {
        if (!m) return;
        if (!m.HasProperty(ShaderUtilities.ID_OutlineWidth)) return; // SDF 셰이더만
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        if (m.HasProperty(ShaderUtilities.ID_OutlineColor)) m.SetColor(ShaderUtilities.ID_OutlineColor, color);
        if (m.HasProperty(ShaderUtilities.ID_FaceDilate)) m.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
    }
    public static void ApplyOutlineToAllMaterials(TMP_Text tmp, Material src)
    {
        if (!tmp || !src) return;
        float ow = src.HasProperty(ShaderUtilities.ID_OutlineWidth) ? src.GetFloat(ShaderUtilities.ID_OutlineWidth) : 0f;
        Color oc = src.HasProperty(ShaderUtilities.ID_OutlineColor) ? src.GetColor(ShaderUtilities.ID_OutlineColor) : Color.black;
        ApplyOutlineToAllMaterials(tmp, ow, oc);
    }
}
