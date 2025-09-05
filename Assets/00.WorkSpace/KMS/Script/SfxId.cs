using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SfxId : int
{
    ButtonClick = 1001,
    ClassicStageEnter = 1002,
    // 1003 (예약)
    ClassicGameOver = 1004,
    ClassicNewRecord = 1005,

    AdvenStageEnter = 1006,
    AdvenFail = 1007,
    AdvenClear = 1008,

    BlockSelect = 1009,
    BlockPlace = 1010,

    // Combo 1~8
    Combo1 = 1011,
    Combo2 = 1012,
    Combo3 = 1013,
    Combo4 = 1014,
    Combo5 = 1015,
    Combo6 = 1016,
    Combo7 = 1017,
    Combo8 = 1018,

    ContinueTimeCheck = 1019,

    // LineClear 1~6
    LineClear1 = 1020,
    LineClear2 = 1021,
    LineClear3 = 1022,
    LineClear4 = 1023,
    LineClear5 = 1024, 
    LineClear6 = 1025,

    // 올클리어는 1030으로 이동
    ClearAllBlock = 1030,
}
