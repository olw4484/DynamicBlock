using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class LayerUtil
{
    /// <summary>루트 포함 모든 자식의 GameObject.layer를 재귀적으로 설정</summary>
    public static void SetLayerRecursive(GameObject root, int layer, bool includeInactive = true)
    {
        if (root == null) return;
        if (layer < 0 || layer > 31)
        {
            Debug.LogWarning($"[LayerUtil] Invalid layer index: {layer}");
            return;
        }

        var transforms = root.GetComponentsInChildren<Transform>(includeInactive);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.layer = layer;
    }

    /// <summary>레이어 이름으로 지정</summary>
    public static void SetLayerRecursive(GameObject root, string layerName, bool includeInactive = true)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
        {
            Debug.LogWarning($"[LayerUtil] Layer '{layerName}' not found.");
            return;
        }
        SetLayerRecursive(root, layer, includeInactive);
    }

    public static void SetLayerRecursiveNoAlloc(Transform root, int layer)
    {
        if (!root) return;
        var stack = new Stack<Transform>(64);
        stack.Push(root);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));
        }
    }
}
