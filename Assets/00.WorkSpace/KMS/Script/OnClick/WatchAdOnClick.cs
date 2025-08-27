using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WatchAdOnClick : MonoBehaviour
{
    [SerializeField] float cooldown = 0.12f; float _cool;
    void Update() { if (_cool > 0f) _cool -= Time.unscaledDeltaTime; }
    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound) return; _cool = cooldown;
        Game.Bus.Publish(new RewardedContinueRequest());
    }
}
