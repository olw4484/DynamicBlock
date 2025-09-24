using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IFx
{
    void PlayRow(int row, Color color);
    void PlayCol(int col, Color color);
    void PlayComboRow(int row, Sprite sprite);
    void PlayComboCol(int col, Sprite sprite);
    void PlayRowPerimeter(int row, Sprite sprite);
    void PlayColPerimeter(int col, Sprite sprite);
    void StopAllLoop();
    void PlayAllClear();
    void PlayAllClearAtWorld(Vector3 pos);
    void PlayGameOverAt();
    void PlayNewScoreAt();
}
