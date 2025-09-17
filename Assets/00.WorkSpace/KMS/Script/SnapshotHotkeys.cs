using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public class SnapshotHotkeys : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6))
            FindFirstObjectByType<SaveManager>()?.SaveRunSnapshot(true); // ���� ������

        if (Input.GetKeyDown(KeyCode.F7))
        {
            var sm = FindFirstObjectByType<SaveManager>();
            sm?.SkipNextSnapshot("Manual restore");
            sm?.TryApplyRunSnapshot(); // ���� ����
        }

        if (Input.GetKeyDown(KeyCode.F10))
            FindFirstObjectByType<SaveManager>()?.ClearRunState(true); // ������ ����
    }
}
#endif
