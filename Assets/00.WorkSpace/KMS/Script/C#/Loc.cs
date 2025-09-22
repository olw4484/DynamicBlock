using System;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class Loc
{
    public static string DefaultTable = "UI";

    public static string Get(string key, params object[] args)
        => LocalizationSettings.StringDatabase.GetLocalizedString(DefaultTable, key, args);

    public static string GetFrom(string table, string key, params object[] args)
        => LocalizationSettings.StringDatabase.GetLocalizedString(table, key, args);

    public static void GetAsync(string table, string key, Action<string> onReady, params object[] args)
    {
        AsyncOperationHandle<string> op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key, args);
        if (op.IsDone) onReady?.Invoke(op.Result);
        else op.Completed += h => onReady?.Invoke(h.Result);
    }
}
