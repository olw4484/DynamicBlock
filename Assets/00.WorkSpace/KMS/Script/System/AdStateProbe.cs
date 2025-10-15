using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AdStateProbe
{
    // ����(Interstitial/Rewarded) ���� ȭ���� ������ ���ȸ� true
    public static bool IsFullscreenShowing { get; set; }

    // ��Ȱ ���(������ ����/��Ȱ ó�� ��)�� �� true
    public static bool IsRevivePending { get; set; }

    // ��ʴ� ������(����Ʈ���� ���� ����)
    public static bool IsBannerShowing { get; set; }

    // [���Ž� ȣȯ] ���� �ڵ尡 IsAdShowing�� ������, ������ Ǯ��ũ���� �ǹ��ϵ��� ����
    public static bool IsAdShowing => IsFullscreenShowing;
}