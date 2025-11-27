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
    [Tooltip("머리에서 아래로 얼마만큼 내려간 곳을 가슴으로 칠 것인가 (미터)")]
    [SerializeField] private float chestYOffset = 0.35f; // 가슴 추정 오프셋 (35cm 아래)

    [Tooltip("가슴(추정 위치)과 손의 거리 허용치 (미터)")]
    [SerializeField] private float chestDistanceThreshold = 0.5f; // 조금 더 여유롭게 0.45 -> 0.5

    [Tooltip("양손 사이의 거리 허용치 (미터)")]
    [SerializeField] private float handsDistanceThreshold = 0.35f; // 조금 더 여유롭게 0.30 -> 0.35

    [Header("Debug Info (Read Only)")]
    [SerializeField] private float currentHandDist; // 현재 양손 거리
    [SerializeField] private float currentLeftDist; // 현재 왼손-가슴 거리
    [SerializeField] private float currentRightDist; // 현재 오른손-가슴 거리
    [SerializeField] private bool isGestureDetected;
    [SerializeField] private bool isButtonOverride;

    // 현재 방어 행동(ABC 자세)이 유효한지 검사합니다. (OR Logic)
    // 1. 제스처가 인식되었거나 (거리 기반)
    // 2. 양쪽 트리거 버튼을 동시에 누르고 있거나 (버튼 기반)
    public bool IsActionValid()
    {
        // 1. 제스처 판정
        isGestureDetected = CheckGestureGeometry();

        // 2. 버튼 판정 (안전장치)
        if (ControllerInputManager.Instance != null)
        {
            isButtonOverride = ControllerInputManager.Instance.IsLeftTriggerHeld &&
                               ControllerInputManager.Instance.IsRightTriggerHeld;
        }

        return isGestureDetected || isButtonOverride;
    }

    // 공간 좌표를 기반으로 ABC 자세 여부를 계산합니다.
    private bool CheckGestureGeometry()
    {
        if (head == null || leftHand == null || rightHand == null) return false;

        // [핵심 수정] 가슴 위치 추정
        // 머리 위치에서 단순히 Y축으로 조금 내린 지점을 가슴으로 가정합니다.
        // HMD가 바라보는 방향과 상관없이 '아래쪽'이 가슴입니다.
        Vector3 chestPosition = head.position - new Vector3(0, chestYOffset, 0);

        // 거리 계산
        currentHandDist = Vector3.Distance(leftHand.position, rightHand.position);
        currentLeftDist = Vector3.Distance(chestPosition, leftHand.position);
        currentRightDist = Vector3.Distance(chestPosition, rightHand.position);

        // 판정
        bool handsClose = currentHandDist < handsDistanceThreshold;
        bool leftClose = currentLeftDist < chestDistanceThreshold;
        bool rightClose = currentRightDist < chestDistanceThreshold;

        return handsClose && leftClose && rightClose;
    }

    public void TriggerHapticFeedback(float intensity)
    {
        // 진동 구현이 필요하다면 여기에 XR Toolkit Haptic 코드를 넣으세요.
    }

    public bool IsHoldingClimbHandle()
    {
        if (ControllerInputManager.Instance != null)
        {
            // 조건 1: 양손 그립 버튼이 모두 눌려있는가? (&& 연산자 사용)
            bool areBothGripsPressed = ControllerInputManager.Instance.IsLeftGripHeld &&
                                       ControllerInputManager.Instance.IsRightGripHeld;

            // 조건 2: 실제로 ClimbHandle 컴포넌트가 달린 물체를 2개 이상 잡고 있는가?
            // ClimbHandle.ActiveGrabCount가 2 이상이어야 함 (양손 그랩)
            bool isGrabbingTwoHandles = ClimbHandle.ActiveGrabCount >= 2;

            // 두 조건이 모두 참이어야 true 반환
            return areBothGripsPressed && isGrabbingTwoHandles;
        }
        return false;
    }

    // [디버깅용] 씬 뷰에서 인식 범위를 눈으로 확인하는 기능
    private void OnDrawGizmos()
    {
        if (head == null) return;

        // 가상 가슴 위치
        Vector3 chestPos = head.position - new Vector3(0, chestYOffset, 0);

        // 1. 가슴 범위 (초록색 구)
        Gizmos.color = new Color(0, 1, 0, 0.3f); // 반투명 초록
        Gizmos.DrawSphere(chestPos, chestDistanceThreshold);

        // 가슴 중심점
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(chestPos, 0.05f);

        // 2. 현재 손 위치가 범위 안에 들어왔는지 선으로 표시
        if (leftHand != null)
        {
            float dist = Vector3.Distance(chestPos, leftHand.position);
            Gizmos.color = dist < chestDistanceThreshold ? Color.green : Color.red;
            Gizmos.DrawLine(chestPos, leftHand.position);
        }

        if (rightHand != null)
        {
            float dist = Vector3.Distance(chestPos, rightHand.position);
            Gizmos.color = dist < chestDistanceThreshold ? Color.green : Color.red;
            Gizmos.DrawLine(chestPos, rightHand.position);
        }
    }
}