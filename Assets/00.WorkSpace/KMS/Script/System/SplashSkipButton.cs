using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SplashSkipButton : MonoBehaviour
{
    public void OnClick()
    {
        if (Game.IsBound) Game.Bus.PublishImmediate(new SplashSkipRequest());
    }
}
