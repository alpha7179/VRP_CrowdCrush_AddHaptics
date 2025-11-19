using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 플레이어의 행동(ABC 자세)을 판정하는 매니저.
/// 센서 기반 제스처 인식과 버튼 기반 안전장치(Fail-safe)를 모두 지원
/// </summary>
public class GestureManager : MonoBehaviour
{
    [Header("Tracked Objects")]
    [SerializeField] private Transform head;      // Main Camera (HMD)
    [SerializeField] private Transform leftHand;  // Left Controller Attach Point
    [SerializeField] private Transform rightHand; // Right Controller Attach Point

    [Header("Detection Settings")]
    [SerializeField] private float chestDistanceThreshold = 0.45f; // 가슴(머리)과 손의 거리 허용치 (단위: m)
    [SerializeField] private float handsDistanceThreshold = 0.30f; // 양손 사이의 거리 허용치 (단위: m)

    // 내부 상태 확인용 (Inspector 디버깅)
    [SerializeField] private bool isGestureDetected;
    [SerializeField] private bool isButtonOverride;

    // 현재 방어 행동(ABC 자세)이 유효한지 검사합니다. (OR Logic)
    // 1. 제스처가 인식되었거나 (거리 기반)
    // 2. 양쪽 트리거 버튼을 동시에 누르고 있거나 (버튼 기반)
    public bool IsActionValid()
    {
        // 1. 제스처 판정
        isGestureDetected = CheckGestureGeometry();

        // 2. 버튼 판정 (ControllerInputManager2 싱글톤 사용)
        // 안전장치: 센서가 튀거나 인식이 안 될 때 버튼으로 대체
        if (ControllerInputManager2.Instance != null)
        {
            isButtonOverride = ControllerInputManager2.Instance.IsLeftTriggerHeld &&
                               ControllerInputManager2.Instance.IsRightTriggerHeld;
        }

        return isGestureDetected || isButtonOverride;
    }

    // 공간 좌표를 기반으로 ABC 자세 여부를 계산합니다.
    private bool CheckGestureGeometry()
    {
        if (head == null || leftHand == null || rightHand == null) return false;

        // 양손이 서로 가까운가?
        float handsDist = Vector3.Distance(leftHand.position, rightHand.position);

        // 손이 머리(가슴) 근처에 있는가?
        // (HMD는 머리에 있으므로, Y축을 조금 아래로 보정해서 가슴 위치를 추정할 수 있음)
        // 여기서는 단순화를 위해 HMD와의 직선 거리를 체크합니다.
        float lDist = Vector3.Distance(head.position, leftHand.position);
        float rDist = Vector3.Distance(head.position, rightHand.position);

        return (handsDist < handsDistanceThreshold) &&
               (lDist < chestDistanceThreshold) &&
               (rDist < chestDistanceThreshold);
    }

    // 진행률에 따라 컨트롤러에 햅틱 진동을 발생시킵니다.
    /// <param name="intensity">0.0 ~ 1.0 (진행률)</param>
    public void TriggerHapticFeedback(float intensity)
    {
        // 진동 세기: 진행될수록 강하게 (0.1 ~ 0.8)
        float amplitude = Mathf.Lerp(0.1f, 0.8f, intensity);
        float duration = 0.1f;

        // XR Input System을 사용하여 햅틱 전송 (구현 필요 시 추가)
        // 예시: ControllerInputManager2.Instance.SendHaptic(amplitude, duration);
    }
}