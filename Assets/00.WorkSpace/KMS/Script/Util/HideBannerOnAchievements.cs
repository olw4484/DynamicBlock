using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideBannerOnAchievements : MonoBehaviour
{
    void OnEnable() => AdManager.Instance?.PushBannerBlock();
    void OnDisable() => AdManager.Instance?.PopBannerBlock();
}
