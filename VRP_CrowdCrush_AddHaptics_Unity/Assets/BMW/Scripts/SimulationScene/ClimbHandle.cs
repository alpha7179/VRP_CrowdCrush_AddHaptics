/// <summary>
/// 등반 가능한 오브젝트(사다리, 암벽 등)에 부착하여 잡고 있는 상태를 추적하는 클래스입니다.
/// <para>
/// 1. XR Interaction Toolkit의 ClimbInteractable을 상속받아 기본적인 등반 기능을 수행합니다.<br/>
/// 2. 플레이어가 잡거나 놓을 때 전역 카운트(ActiveGrabCount)를 갱신합니다.<br/>
/// 3. 잡았을 때 해당 컨트롤러에 햅틱 피드백을 발생시킵니다.
/// </para>
/// </summary>

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors; // XRBaseInputInteractor 네임스페이스

/// <summary>
/// 등반 기능과 잡기 상태 추적 및 햅틱 피드백을 제공합니다.
/// </summary>
public class ClimbHandle : UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing.ClimbInteractable
{
    #region Global State
    public static int ActiveGrabCount = 0;
    #endregion

    #region Inspector Settings
    [Header("Haptic Settings")]
    [Tooltip("잡았을 때 진동의 세기 (0.0 ~ 1.0)")]
    [SerializeField][Range(0, 1)] private float hapticIntensity = 0.5f;
    [Tooltip("잡았을 때 진동의 지속 시간 (초)")]
    [SerializeField] private float hapticDuration = 0.1f;
    #endregion

    #region Interaction Events
    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        ActiveGrabCount++;
        TriggerHaptic(args.interactorObject);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        ActiveGrabCount--;
        if (ActiveGrabCount < 0) ActiveGrabCount = 0;
    }
    #endregion

    #region Unity Lifecycle
    protected override void OnDisable()
    {
        base.OnDisable();
        if (isSelected)
        {
            ActiveGrabCount--;
            if (ActiveGrabCount < 0) ActiveGrabCount = 0;
        }
    }
    #endregion

    #region Haptic Logic
    private void TriggerHaptic(IXRSelectInteractor interactor)
    {
        // [진동 정규화 적용]
        float finalIntensity = hapticIntensity;
        if (DataManager.Instance != null)
        {
            finalIntensity = DataManager.Instance.GetAdjustedHapticStrength(hapticIntensity);
        }

        if (finalIntensity <= 0.01f) return;

        // [경고 해결] XRBaseControllerInteractor -> XRBaseInputInteractor 변경
        if (interactor is XRBaseInputInteractor inputInteractor)
        {
            inputInteractor.SendHapticImpulse(finalIntensity, hapticDuration);
        }
    }
    #endregion
}