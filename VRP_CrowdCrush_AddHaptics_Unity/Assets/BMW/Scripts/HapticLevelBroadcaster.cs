using UnityEngine;
using Bhaptics.SDK2;

public class HapticLevelBroadcaster : MonoBehaviour
{
    // 8방향 prefix들을 배열로 관리
    private readonly string[] directionPrefixes = new string[]
    {
        "b_right",
        "b_left",
        "f_right",
        "f_left",
        "front",
        "back",
        "right",
        "left"
    };

    // 외부에서 level(1~6)을 넘겨 호출하는 함수
    public void PlayLevel(int level)
    {
        // 범위 체크 (1~6 아니면 무시하거나 Clamp)
        if (level < 1 || level > 6)
        {
            Debug.LogWarning($"[HAPTIC LEVEL] 잘못된 레벨: {level}. 1~6 사이여야 합니다.");
            return;
        }

        foreach (var prefix in directionPrefixes)
        {
            string eventId = $"{prefix}_{level}"; // 예: "left_6"

            Debug.Log($"[HAPTIC LEVEL] Play {eventId}");
            BhapticsLibrary.Play(eventId);
        }
    }

    // 🔹 Unity UI Button에서 쓰기 편하게, 인스펙터에서 level 설정해서 쓰는 버전
    [Header("Button에서 사용할 기본 레벨")]
    [Range(1, 6)]
    public int defaultLevel = 1;

    public void PlayDefaultLevel()
    {
        PlayLevel(defaultLevel);
    }
}