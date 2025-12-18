using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// 등반 가능한 오브젝트에 부착하여 잡기 상태를 관리하는 클래스입니다.
/// </summary>
public class ClimbHandle : UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing.ClimbInteractable
{
    #region Global State
    // 전역에서 현재 잡고 있는 핸들 개수를 추적
    public static int ActiveGrabCount = 0;
    #endregion

    #region Inspector Settings
    [Header("Haptic Settings")]
    [Tooltip("잡았을 때 진동의 세기 (0.0 ~ 1.0)")]
    [SerializeField][Range(0, 1)] private float hapticIntensity = 0.5f;
    [Tooltip("잡았을 때 진동의 지속 시간 (초)")]
    [SerializeField] private float hapticDuration = 0.1f;
    #endregion

    #region Unity Lifecycle

    // [추가] 씬이 시작되거나 오브젝트가 켜질 때 카운트 안전장치
    // 주의: 만약 씬 전환 시에도 잡고 있는 상태를 유지해야 한다면 이 부분은 조정이 필요할 수 있습니다.
    // 하지만 일반적인 경우, 새로 로드되면 0에서 시작하는 것이 안전합니다.
    protected override void Awake()
    {
        base.Awake();
        // 씬 로드 시 혹시 남아있을 수 있는 static 값 초기화 (선택 사항)
        // ActiveGrabCount = 0; 
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // 비활성화될 때(예: 파괴되거나 꺼질 때) 잡고 있었다면 카운트 감소
        if (isSelected)
        {
            DecreaseGrabCount();
        }
    }
    #endregion

    #region Interaction Events

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        ActiveGrabCount++;
        TriggerHaptic(args.interactorObject);

        // 디버그용: 실제로 잡혔는지 확인
        // Debug.Log($"[ClimbHandle] Grabbed! Count: {ActiveGrabCount}");
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        DecreaseGrabCount();

        // 디버그용: 놓쳤을 때 로그
        // Debug.Log($"[ClimbHandle] Released. Count: {ActiveGrabCount}");
    }

    #endregion

    #region Logic & Helpers

    private void DecreaseGrabCount()
    {
        ActiveGrabCount--;
        if (ActiveGrabCount < 0) ActiveGrabCount = 0;
    }

    private void TriggerHaptic(IXRSelectInteractor interactor)
    {
        // [진동 정규화 적용]
        float finalIntensity = hapticIntensity;
        if (DataManager.Instance != null)
        {
            finalIntensity = DataManager.Instance.GetAdjustedHapticStrength(hapticIntensity);
        }

        if (finalIntensity <= 0.01f) return;

        if (interactor is XRBaseInputInteractor inputInteractor)
        {
            inputInteractor.SendHapticImpulse(finalIntensity, hapticDuration);
        }
    }
    #endregion
}