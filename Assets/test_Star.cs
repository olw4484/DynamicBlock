using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class test_Star : MonoBehaviour
{
    string star;

    void Start()
    {
        Phase1();
        Phase2();
        Phase3();
        Phase4();
        Phase5();
    }

    public void Phase1()
    {
        star = string.Empty;
        int height = 5;

        for (int i = 1; i <= height; i++)
        {
            for (int j = 0; j < i; j++)
            {
                star += "★";
            }
            star += "\n";
        }

        Debug.Log("Phase 1\n" + star);
    }

    public void Phase2()
    {
        star = string.Empty;
        int height = 5;

        for (int i = 1; i <= height; i++)
        {
            for (int j = 0; j < height - i; j++)
            {
                star += "　"; // 전각 공백
            }
            for (int k = 0; k < i; k++)
            {
                star += "★";
            }
            star += "\n";
        }

        Debug.Log("Phase 2\n" + star);
    }

    public void Phase3()
    {
        star = string.Empty;
        int height = 5;

        for (int i = 1; i <= height; i++)
        {
            for (int j = 0; j < i; j++)
            {
                star += "★";
            }
            star += "\n";
        }

        for (int i = height - 1; i >= 1; i--)
        {
            for (int j = 0; j < i; j++)
            {
                star += "★";
            }
            star += "\n";
        }

        Debug.Log("Phase 3\n" + star);
    }

    public void Phase4()
    {
        star = string.Empty;
        int height = 5;

        for (int i = 1; i <= height; i++)
        {
            for (int j = 0; j < height - i; j++)
            {
                star += "　";
            }
            for (int k = 0; k < i; k++)
            {
                star += "★";
            }
            star += "\n";
        }

        for (int i = height - 1; i >= 1; i--)
        {
            for (int j = 0; j < height - i; j++)
            {
                star += "　";
            }
            for (int k = 0; k < i; k++)
            {
                star += "★";
            }
            star += "\n";
        }

        Debug.Log("Phase 4\n" + star);
    }

    public void Phase5()
    {
        star = string.Empty;
        int height = 5;

        // 위쪽 피라미드
        for (int i = 1; i <= height; i++)
        {
            for (int j = 0; j < height - i; j++)
            {
                star += "　";
            }
            for (int k = 0; k < 2 * i - 1; k++)
            {
                star += "★";
            }
            star += "\n";
        }

        // 아래쪽 역피라미드
        for (int i = height - 1; i >= 1; i--)
        {
            for (int j = 0; j < height - i; j++)
            {
                star += "　";
            }
            for (int k = 0; k < 2 * i - 1; k++)
            {
                star += "★";
            }
            star += "\n";
        }

        Debug.Log("Phase 5\n" + star);
    }
}