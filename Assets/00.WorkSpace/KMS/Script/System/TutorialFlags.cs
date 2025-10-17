using UnityEngine;

public static class TutorialFlags
{
    const string kHandInjected = "tut_hand_injected";
    const string kFirstPlaced = "tut_first_placed";
    const string kTutorialDone = "tut_done";

    public static bool WasTutorialHandInjected() => PlayerPrefs.GetInt(kHandInjected, 0) == 1;
    public static void MarkHandInjected() { PlayerPrefs.SetInt(kHandInjected, 1); PlayerPrefs.Save(); }

    public static bool WasFirstPlacement() => PlayerPrefs.GetInt(kFirstPlaced, 0) == 1;
    public static void MarkFirstPlacement() { PlayerPrefs.SetInt(kFirstPlaced, 1); PlayerPrefs.Save(); }

    public static bool WasTutorialDone() => PlayerPrefs.GetInt(kTutorialDone, 0) == 1;
    public static void MarkTutorialDone() { PlayerPrefs.SetInt(kTutorialDone, 1); PlayerPrefs.Save(); }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(kHandInjected);
        PlayerPrefs.DeleteKey(kFirstPlaced);
        PlayerPrefs.DeleteKey(kTutorialDone);
    }
}
public static class TutorialRuntime
{
    public static bool FirstHandDealtThisSession = false;
    public static void Reset() => FirstHandDealtThisSession = false;
}