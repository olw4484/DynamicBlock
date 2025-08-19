using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LineRemover : MonoBehaviour
{
    public void ClearLine(int width, int height, object[,] board)
    {
        for (int y = 0; y < height; y++)
        {
            bool fullRow = true;
            for (int x = 0; x < width; x++)
            {
                if (board[x, y] == null)
                {
                    fullRow = false;
                    break;
                }
            }

            if (fullRow)
            {
                for (int x = 0; x < width; x++) board[x, y] = null;
            }
        }
        
        for (int x = 0; x < width; x++)
        {
            bool fullCol = true;
            for (int y = 0; y < height; y++)
            {
                if (board[x, y] == null)
                {
                    fullCol = false;
                    break;
                }
            }

            if (fullCol)
            {
                for (int y = 0; y < height; y++) board[x, y] = null;
            }
        }
    }
}
