using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;

/// <summary>
/// 등반 가능한 오브젝트(사다리, 암벽 등)에 부착하여 잡고 있는 상태를 추적하는 클래스입니다.
/// <para>
/// 1. XR Interaction Toolkit의 ClimbInteractable을 상속받아 기본적인 등반 기능을 수행합니다.<br/>
/// 2. 플레이어가 잡거나 놓을 때 전역 카운트(ActiveGrabCount)를 갱신합니다.<br/>
/// 3. GestureManager에서 이 카운트를 참조하여 '양손으로 매달려 있는지' 판정합니다.
/// </para>
/// </summary>
public class ClimbHandle : ClimbInteractable
{
    #region Global State

    /// <summary>
    /// 현재 씬에서 플레이어가 잡고 있는 모든 ClimbHandle의 총 개수입니다.
    /// (이 값이 2 이상이면 양손으로 매달린 것으로 간주)
    /// </summary>
    public static int ActiveGrabCount = 0;

    #endregion

    #region Interaction Events

    /// <summary>
    /// 플레이어가 핸들을 잡았을 때 호출됩니다.
    /// </summary>
    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args); // 부모 클래스의 등반 로직 실행

        // 잡은 핸들 수 증가
        ActiveGrabCount++;

        // 디버그 로그 (필요 시 주석 처리)
        // Debug.Log($"[ClimbHandle] Grabbed. Total Count: {ActiveGrabCount}");
    }

    /// <summary>
    /// 플레이어가 핸들을 놓았을 때 호출됩니다.
    /// </summary>
    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args); // 부모 클래스의 로직 실행

        // 잡은 핸들 수 감소
        ActiveGrabCount--;

        // 안전장치: 음수가 되지 않도록 보정
        if (ActiveGrabCount < 0) ActiveGrabCount = 0;
    }

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 오브젝트가 비활성화되거나 파괴될 때 예외 처리를 수행합니다.
    /// (잡은 상태로 오브젝트가 사라지면 카운트가 영원히 남는 문제 방지)
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();

        // 만약 누군가 잡고 있는 상태에서 비활성화되었다면 카운트 차감
        if (isSelected)
        {
            ActiveGrabCount--;
            if (ActiveGrabCount < 0) ActiveGrabCount = 0;
        }
    }

    #endregion
}