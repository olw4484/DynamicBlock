using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LJJ
{
    public static class GameEvents
    {
        public static Action<int, Color, bool> OnBlockDestroyed;
    }
}
