using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

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
    #region Inspector Settings
    [Header("Target References")]
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform leftHandTransform;
    [SerializeField] private Transform rightHandTransform;

    [Header("Detection Settings")]
    [SerializeField] private float detectionDistance = 0.4f;
    [SerializeField] private float triggerThreshold = 0.8f;

    [Header("Fail-Safe Settings")]
    [Tooltip("체크 시: 거리 범위 밖이라도 트리거만 당기면 액션을 성공으로 처리합니다.")]
    [SerializeField] private bool useTriggerFailSafe = true; // [추가됨] 강제 발동 옵션

    [Header("Feedback Settings")]
    [SerializeField] private SFXType rangeEnterSFX = SFXType.UI_Click;
    [SerializeField] private float rangeEnterHapticIntensity = 0.3f;
    [SerializeField] private float rangeEnterHapticDuration = 0.1f;
    [SerializeField] private float holdingHapticIntensity = 0.05f;
    #endregion

    #region Internal State
    private bool isInRange = false;
    private bool isActionValid = false;
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        // 1. 거리 체크 (범위 진입 피드백용)
        bool currentRangeCheck = CheckHandsNearHead();

        // 범위에 새로 들어왔을 때만 피드백 재생
        if (currentRangeCheck && !isInRange)
        {
            PlayRangeEnterFeedback();
        }

        isInRange = currentRangeCheck;

        // 2. 트리거 체크
        bool triggersPressed = CheckTriggersPressed();

        // 3. 최종 판정 로직 [수정됨]
        // 조건: (범위 내에 있음 OR 강제 발동 허용됨) AND 트리거 당김
        if ((isInRange || useTriggerFailSafe) && triggersPressed)
        {
            isActionValid = true;
            TriggerContinuousHaptic(holdingHapticIntensity);
        }
        else
        {
            isActionValid = false;
        }
    }
    #endregion

    #region Logic Methods
    private bool CheckHandsNearHead()
    {
        if (headTransform == null || leftHandTransform == null || rightHandTransform == null) return false;
        float distLeft = Vector3.Distance(headTransform.position, leftHandTransform.position);
        float distRight = Vector3.Distance(headTransform.position, rightHandTransform.position);
        return (distLeft <= detectionDistance) && (distRight <= detectionDistance);
    }

    private bool CheckTriggersPressed()
    {
        bool leftTrigger = false;
        bool rightTrigger = false;
        var leftDevices = new List<InputDevice>();
        var rightDevices = new List<InputDevice>();

        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, leftDevices);
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, rightDevices);

        if (leftDevices.Count > 0) { leftDevices[0].TryGetFeatureValue(CommonUsages.trigger, out float val); if (val > triggerThreshold) leftTrigger = true; }
        if (rightDevices.Count > 0) { rightDevices[0].TryGetFeatureValue(CommonUsages.trigger, out float val); if (val > triggerThreshold) rightTrigger = true; }

        return leftTrigger && rightTrigger;
    }
    #endregion

    #region Feedback Methods
    private void PlayRangeEnterFeedback()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(rangeEnterSFX);
        TriggerImpulseHaptic(rangeEnterHapticIntensity, rangeEnterHapticDuration);
    }

    private void TriggerImpulseHaptic(float rawIntensity, float duration)
    {
        // [진동 정규화 적용]
        float finalIntensity = rawIntensity;
        if (DataManager.Instance != null)
        {
            finalIntensity = DataManager.Instance.GetAdjustedHapticStrength(rawIntensity);
        }

        if (finalIntensity <= 0.01f) return;

        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);

        foreach (var device in devices)
        {
            if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
            {
                device.SendHapticImpulse(0, finalIntensity, duration);
            }
        }
    }

    private void TriggerContinuousHaptic(float intensity)
    {
        TriggerImpulseHaptic(intensity, 0.1f);
    }
    #endregion

    #region Public API
    public bool IsActionValid()
    {
        return isActionValid;
    }

    public bool IsHoldingClimbHandle()
    {
        return ClimbHandle.ActiveGrabCount > 0;
    }
    #endregion
}