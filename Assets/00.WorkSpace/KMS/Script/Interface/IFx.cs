using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IFx
{
    void PlayRow(int row, Color color);
    void PlayCol(int col, Color color);
    void PlayRowPerimeter(int row, Color color);
    void PlayColPerimeter(int col, Color color);
    void PlayAllClear();
    void PlayNewScore();
    void PlayGameOver();
}
