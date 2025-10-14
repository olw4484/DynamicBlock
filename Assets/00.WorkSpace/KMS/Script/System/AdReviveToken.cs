using System;
using System.Globalization;
using UnityEngine;

public static class AdReviveToken
{
    const string KEY = "revive_after_rewarded_at_utc";

    public static void MarkGranted()
    {
        PlayerPrefs.SetString(KEY, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    public static bool HasPending()
    {
        return PlayerPrefs.HasKey(KEY);
    }

    // seconds 내로만 유효
    public static bool ConsumeIfFresh(double seconds = 180.0)
    {
        if (!PlayerPrefs.HasKey(KEY)) return false;
        var s = PlayerPrefs.GetString(KEY, "");
        PlayerPrefs.DeleteKey(KEY); // 한 번 읽으면 바로 제거
        PlayerPrefs.Save();

        if (string.IsNullOrEmpty(s)) return false;
        if (!DateTime.TryParse(s, null, DateTimeStyles.RoundtripKind, out var at)) return false;

        return (DateTime.UtcNow - at).TotalSeconds <= seconds;
    }
}
