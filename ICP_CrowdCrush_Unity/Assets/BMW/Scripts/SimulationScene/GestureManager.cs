using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 플레이어의 특정 행동(예: ABC 방어 자세)을 판정하는 매니저 클래스입니다.
/// <para>
/// 1. 머리와 양손의 위치 관계를 계산하여 제스처를 인식합니다.<br/>
/// 2. 인식률 저하를 대비해 컨트롤러 버튼(Trigger)을 이용한 강제 발동(Fail-safe)을 지원합니다.<br/>
/// 3. 등반(Climbing) 상태인지 판별하는 로직을 포함합니다.
/// </para>
/// </summary>
public class GestureManager : MonoBehaviour
{
    #region Inspector Settings (Tracked Objects)

    [Header("Tracked Objects")]
    [Tooltip("플레이어의 머리(Main Camera) Transform")]
    [SerializeField] private Transform head;

    [Tooltip("왼손 컨트롤러의 위치 (Attach Point)")]
    [SerializeField] private Transform leftHand;

    [Tooltip("오른손 컨트롤러의 위치 (Attach Point)")]
    [SerializeField] private Transform rightHand;

    #endregion

    #region Inspector Settings (Detection Parameters)

    [Header("Detection Settings")]
    [Tooltip("머리 위치에서 아래로 얼마만큼 내려간 곳을 '가슴'으로 추정할지 설정 (단위: m)")]
    [SerializeField] private float chestYOffset = 0.35f;

    [Tooltip("가슴(추정 위치)과 손 사이의 최대 허용 거리 (이 거리 안으로 손이 들어와야 함)")]
    [SerializeField] private float chestDistanceThreshold = 0.5f;

    [Tooltip("양손 사이의 최대 허용 거리 (양손을 모았는지 판별)")]
    [SerializeField] private float handsDistanceThreshold = 0.35f;

    #endregion

    #region Debug Info

    [Header("Debug Info (Read Only)")]
    [Tooltip("현재 양손 사이의 거리")]
    [SerializeField] private float currentHandDist;

    [Tooltip("현재 양손 사이의 거리")]
    [SerializeField] private bool isOnlyButtonDetected = true;

    [Tooltip("현재 가슴과 왼손 사이의 거리")]
    [SerializeField] private float currentLeftDist;

    [Tooltip("현재 가슴과 오른손 사이의 거리")]
    [SerializeField] private float currentRightDist;

    [Tooltip("제스처(거리 기반) 인식 성공 여부")]
    [SerializeField] private bool isGestureDetected;

    [Tooltip("버튼(트리거) 입력을 통한 강제 인식 여부")]
    [SerializeField] private bool isButtonOverride;

    #endregion

    #region Public API (Action Checks)

    /// <summary>
    /// 현재 방어 행동(ABC 자세)이 유효한지 검사합니다.
    /// <para>제스처가 인식되었거나(OR), 양쪽 트리거 버튼을 동시에 누르고 있으면 true를 반환합니다.</para>
    /// </summary>
    public bool IsActionValid()
    {
        // 1. 공간 좌표 기반 제스처 판정
        isGestureDetected = CheckGestureGeometry();

        // 2. 버튼 입력 기반 안전장치 (Fail-safe)
        if (ControllerInputManager.Instance != null)
        {
            // 양쪽 트리거를 모두 당기고 있는지 확인
            isButtonOverride = ControllerInputManager.Instance.IsLeftTriggerHeld &&
                               ControllerInputManager.Instance.IsRightTriggerHeld;
        }

        // 둘 중 하나라도 만족하면 유효한 행동으로 간주
        if (!isOnlyButtonDetected) return isGestureDetected || isButtonOverride;
        else return isButtonOverride;
    }

    /// <summary>
    /// 플레이어가 현재 '등반(Climbing)' 핸들을 잡고 있는지 확인합니다.
    /// </summary>
    /// <returns>양손 그립 버튼을 누르고 있고, 실제로 2개 이상의 핸들을 잡고 있다면 true</returns>
    public bool IsHoldingClimbHandle()
    {
        if (ControllerInputManager.Instance != null)
        {
            // 조건 1: 양손 그립 버튼(Select)이 모두 눌려있는가?
            bool areBothGripsPressed = ControllerInputManager.Instance.IsLeftGripHeld &&
                                       ControllerInputManager.Instance.IsRightGripHeld;

            // 조건 2: 실제로 ClimbHandle 컴포넌트가 달린 물체를 2개 이상 잡고 있는가?
            // (ClimbHandle.ActiveGrabCount는 ClimbHandle 클래스에서 관리하는 정적 변수로 가정)
            bool isGrabbingTwoHandles = ClimbHandle.ActiveGrabCount >= 2;

            return areBothGripsPressed && isGrabbingTwoHandles;
        }
        return false;
    }

    /// <summary>
    /// 컨트롤러에 햅틱 피드백(진동)을 발생시킵니다.
    /// </summary>
    /// <param name="intensity">진동 강도 (0.0 ~ 1.0)</param>
    public void TriggerHapticFeedback(float intensity)
    {
        // TODO: XR Interaction Toolkit의 Haptic 기능을 사용하여 구현
        // 예: controller.SendHapticImpulse(intensity, duration);
    }

    #endregion

    #region Internal Logic (Geometry Calculation)

    /// <summary>
    /// 머리, 양손의 좌표를 계산하여 제스처(ABC 자세)가 맞는지 판별합니다.
    /// </summary>
    private bool CheckGestureGeometry()
    {
        // 필수 추적 대상이 없으면 판정 불가
        if (head == null || leftHand == null || rightHand == null) return false;

        // 1. 가슴 위치 추정
        // HMD(머리) 위치에서 단순히 Y축으로 일정 거리(chestYOffset) 내린 지점을 가슴으로 가정합니다.
        // 회전을 고려하지 않는 이유는 사용자가 고개를 돌려도 가슴 위치는 유지되는 경우가 많기 때문입니다.
        Vector3 chestPosition = head.position - new Vector3(0, chestYOffset, 0);

        // 2. 거리 계산
        currentHandDist = Vector3.Distance(leftHand.position, rightHand.position);      // 양손 간 거리
        currentLeftDist = Vector3.Distance(chestPosition, leftHand.position);           // 가슴-왼손 거리
        currentRightDist = Vector3.Distance(chestPosition, rightHand.position);         // 가슴-오른손 거리

        // 3. 임계값 비교 판정
        bool handsClose = currentHandDist < handsDistanceThreshold;     // 양손이 모였는가?
        bool leftClose = currentLeftDist < chestDistanceThreshold;      // 왼손이 가슴 근처인가?
        bool rightClose = currentRightDist < chestDistanceThreshold;    // 오른손이 가슴 근처인가?

        // 세 조건이 모두 충족되어야 제스처 인정
        return handsClose && leftClose && rightClose;
    }

    #endregion

    #region Debugging (Gizmos)

    /// <summary>
    /// 씬 뷰(Scene View)에서 인식 범위를 시각적으로 확인하기 위한 기즈모를 그립니다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (head == null) return;

        // 추정된 가슴 위치
        Vector3 chestPos = head.position - new Vector3(0, chestYOffset, 0);

        // 1. 가슴 인식 범위 표시 (반투명 초록색 구)
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawSphere(chestPos, chestDistanceThreshold);

        // 가슴 중심점 표시 (빨간 와이어 구)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(chestPos, 0.05f);

        // 2. 현재 손 위치와 가슴 사이의 거리 및 인식 상태 표시
        DrawHandGizmo(leftHand, chestPos);
        DrawHandGizmo(rightHand, chestPos);
    }

    /// <summary>
    /// 각 손에 대한 기즈모(선)를 그립니다. 인식 범위 내에 있으면 초록색, 밖이면 빨간색으로 표시됩니다.
    /// </summary>
    private void DrawHandGizmo(Transform hand, Vector3 targetPos)
    {
        if (hand != null)
        {
            float dist = Vector3.Distance(targetPos, hand.position);

            // 인식 범위 안이면 초록색, 밖이면 빨간색
            Gizmos.color = dist < chestDistanceThreshold ? Color.green : Color.red;

            Gizmos.DrawLine(targetPos, hand.position);
        }
    }

    #endregion
}