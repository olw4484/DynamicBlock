using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GachaSystem : MonoBehaviour
{
    public TMP_Text resultTextUI;     // 뽑기 결과 출력
    public TMP_Text countTextUI;      // 뽑기 횟수 출력
    public TMP_Text gachungTextUI;    // 각청 축하 메시지 출력

    private int drawCount = 0;

    public void PerformGacha()
    {
        List<string> resultList = new List<string>();
        bool gotGachung = false;

        // 각청 축하 텍스트 초기화
        gachungTextUI.text = "";

        for (int i = 0; i < 10; i++)
        {
            int randomValue = Random.Range(1, 101);
            string character = "";

            if (randomValue <= 5)
                character = "각청";
            else if (randomValue <= 20)
                character = "모나";
            else if (randomValue <= 40)
                character = "치치";
            else if (randomValue <= 60)
                character = "향릉";
            else if (randomValue <= 80)
                character = "신염";
            else if (randomValue <= 90)
                character = "노엘";
            else if (randomValue <= 95)
                character = "바바라";
            else if (randomValue <= 98)
                character = "엠버";
            else
                character = "리사";

            resultList.Add(character);

            if (character == "각청")
                gotGachung = true;
        }

        drawCount++;
        if (drawCount % 10 == 0)
        {
            resultList.Add("각청");
            gotGachung = true;
        }

        resultTextUI.text = $" 10연차 결과:\n{string.Join(", ", resultList)}";

        if (gotGachung)
        {
            gachungTextUI.text = " 축하합니다! '각청'을 뽑았습니다!";
            drawCount = 0;
        }

        countTextUI.text = $" 현재까지 10연차 뽑기 횟수: {drawCount}회";
    }
}
