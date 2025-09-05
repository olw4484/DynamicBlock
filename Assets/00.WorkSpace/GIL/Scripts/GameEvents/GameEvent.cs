using System;
using System.Collections.Generic;
using UnityEngine;

namespace _00.WorkSpace.GIL.Scripts.GameEvents
{
    public class GameEvent : MonoBehaviour
    {
        public static Func<List<Transform>, bool> CheckIfShapeCanBePlaced;
    }
}
