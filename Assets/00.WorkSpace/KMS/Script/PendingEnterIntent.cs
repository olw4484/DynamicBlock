using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PendingEnterIntent
{
    private static bool _has;
    private static GameEnterIntent _intent;

    public static void Set(GameEnterIntent intent)
    {
        _intent = intent;
        _has = true;
    }

    public static bool TryConsume(out GameEnterIntent intent)
    {
        if (_has)
        {
            intent = _intent;
            _has = false;            // 한 번만 소비
            return true;
        }
        intent = default;
        return false;
    }
}