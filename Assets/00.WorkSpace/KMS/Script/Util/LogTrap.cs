using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogTrap : MonoBehaviour
{
    void OnEnable() => Application.logMessageReceived += Hook;
    void OnDisable() => Application.logMessageReceived -= Hook;

    void Hook(string condition, string stackTrace, LogType type)
    {
        if (condition.Contains("Prefab") || condition.Contains("No Prefab"))
        {
            Debug.LogError($"[Trap] Who logged this? >>> {condition}\n{stackTrace}");
        }
    }
}
