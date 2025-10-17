using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnalyticsBridge : MonoBehaviour
{
    void OnEnable()
    {
        Game.Bus?.Subscribe<AdventureBestUpdated>(OnBestUpdated, replaySticky: false);
    }
    void OnDisable()
    {
        Game.Bus?.Unsubscribe<AdventureBestUpdated>(OnBestUpdated);
    }
    void OnBestUpdated(AdventureBestUpdated e)
    {
        AnalyticsManager.Instance?.AdventureBestLog(e.newIndex, e.stageName);
    }
}
