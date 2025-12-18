using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// 플레이어가 특정 구역(Zone)에 진입했는지 감지하여 게임 진행을 제어하는 트리거입니다.
/// <para>
/// 1. Box Collider(Is Trigger 체크 필수)가 있는 오브젝트에 컴포넌트를 추가해야 합니다.<br/>
/// 2. 'Goal' 설정 시 다음 시나리오 단계로 넘어가도록 GameStepManager에 신호를 보냅니다.<br/>
/// 3. 'Danger' 설정 시 실수 횟수를 증가시키고 플레이어를 원래 위치로 되돌립니다.
/// </para>
/// </summary>

public class ZoneTrigger : MonoBehaviour
{
    #region Inspector Settings
    [Header("Trigger Settings")]
    [SerializeField] private bool isGoal = true;
    [SerializeField] private bool isDanger = false;
    [Header("Target Settings")]
    [SerializeField] private string playerTag = "Player";
    [Header("Haptic Settings")]
    [SerializeField][Range(0, 1)] private float hapticIntensity = 0.5f;
    [SerializeField] private float hapticDuration = 0.2f;
    [Header("Debug")]
    [SerializeField] private bool isDebug = true;
    #endregion

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag) || (other.transform.root != null && other.transform.root.CompareTag(playerTag)))
        {
            if (isDebug) Debug.Log($"[ZoneTrigger] Player entered: {gameObject.name}");
            HandlePlayerEnter();
        }
    }

    private void HandlePlayerEnter()
    {
        TriggerZoneHaptic();

        var stepManager = FindAnyObjectByType<GameStepManager>();
        if (stepManager != null)
        {
            if (isGoal)
            {
                if (isDebug) Debug.Log($"[ZoneTrigger] Goal: {gameObject.name}");
                stepManager.SetZoneReached(true);
            }
            else if (isDanger)
            {
                if (isDebug) Debug.Log($"[ZoneTrigger] Danger: {gameObject.name}");
                if (DataManager.Instance != null) DataManager.Instance.AddMistakeCount();
                stepManager.ReturnToSavedPosition();
            }
        }
    }

    private void TriggerZoneHaptic()
    {
        // [진동 정규화 적용]
        float finalIntensity = hapticIntensity;
        if (DataManager.Instance != null)
        {
            finalIntensity = DataManager.Instance.GetAdjustedHapticStrength(hapticIntensity);
        }

        if (finalIntensity <= 0.01f) return;

        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);

        foreach (var device in devices)
        {
            if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
            {
                device.SendHapticImpulse(0, finalIntensity, hapticDuration);
            }
        }
    }
}