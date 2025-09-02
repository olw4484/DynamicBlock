using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OptionMenu : MonoBehaviour
{
    public void OnBGMVolumeChanged(float value)
    {
        AudioManager.Instance.SetBGMVolume(value);
    }

    public void OnSEVolumeChanged(float value)
    {
        AudioManager.Instance.SetSEVolume(value);
    }
}