using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;
using static GameManager;

public class GameStepManager : MonoBehaviour
{
    // ... Inspector Settings ...
    #region Inspector Settings (References)
    [Header("Player References")]
    [SerializeField] private Transform PlayerTransform;
    [Header("Linked Managers")]
    [SerializeField] private IngameUIManager uiManager;
    [SerializeField] private GestureManager gestureManager;
    [Header("Zone Objects")]
    [SerializeField] private GameObject[] TargerZone;
    #endregion

    #region Inspector Settings (Game Logic)
    [Header("Action Settings")]
    [SerializeField] private float targetHoldTime = 3.0f;
    [Header("Timing Settings")]
    [SerializeField] private float phaseTime = 60.0f;
    [SerializeField] private float instructionDuration = 5.0f;
    [SerializeField] private float feedbackDuration = 5.0f;
    [SerializeField] private float nextStepDuration = 1.0f;
    #endregion

    #region Internal State
    public enum GamePhase { Caution, Tutorial, Move1, ABCPose, Move2, HoldPillar, ClimbUp, Escape, Finished, Null }
    [Header("Debug Info")]
    [SerializeField] private GamePhase currentPhase;
    private bool isZoneReached = false;
    private bool isActionCompleted = false;
    private float currentActionHoldTimer = 0f;
    private int targetIndex;
    private Vector3 startPosition;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        // [중요] 시작하자마자 이동 끄기
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetLocomotion(false);
        StartCoroutine(ScenarioRoutine());
    }
    #endregion

    // ... (Public API, Haptic, Audio Helper 등은 기존과 동일) ...
    #region Public API
    public void SetZoneReached(bool reached) { isZoneReached = reached; }
    public void SavePlayerPosition() { if (PlayerTransform != null) startPosition = PlayerTransform.position; }
    public void ReturnToSavedPosition() { StopCoroutine(ReturnToSavedPositionRoutine()); StartCoroutine(ReturnToSavedPositionRoutine()); }
    #endregion

    #region Haptic & Audio Helpers
    private void TriggerHaptic(float rawAmplitude, float duration)
    {
        float finalAmplitude = rawAmplitude;
        if (DataManager.Instance != null) finalAmplitude = DataManager.Instance.GetAdjustedHapticStrength(rawAmplitude);
        if (finalAmplitude <= 0.01f) return;
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        foreach (var device in devices) if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse) device.SendHapticImpulse(0, finalAmplitude, duration);
    }

    private void UpdateAmbience(GamePhase phase)
    {
        if (AudioManager.Instance == null) return;
        switch (phase)
        {
            case GamePhase.Tutorial: case GamePhase.Move1: AudioManager.Instance.PlayAMB(AMBType.Crowd, 0); break;
            case GamePhase.ABCPose: case GamePhase.Move2: case GamePhase.HoldPillar: AudioManager.Instance.PlayAMB(AMBType.Crowd, 1); break;
            case GamePhase.ClimbUp: case GamePhase.Escape: AudioManager.Instance.PlayAMB(AMBType.Crowd, 2); break;
        }
        if (phase >= GamePhase.Move2 && phase != GamePhase.Finished)
        {
            AudioManager.Instance.PlayShuffleSFX(SFXType.Police, true);
            AudioManager.Instance.PlaySFX(SFXType.Ambulance, true, true);
            float policeVol = 0.5f; float ambulanceVol = 0.5f;
            if (phase == GamePhase.HoldPillar) { policeVol = 0.7f; ambulanceVol = 0.7f; }
            else if (phase == GamePhase.ClimbUp) { policeVol = 0.9f; ambulanceVol = 0.9f; }
            else if (phase == GamePhase.Escape) { policeVol = 1.0f; ambulanceVol = 1.0f; }
            AudioManager.Instance.SetLoopingSFXScale(SFXType.Police, policeVol);
            AudioManager.Instance.SetLoopingSFXScale(SFXType.Ambulance, ambulanceVol);
        }
    }
    #endregion

    #region UI & Logic Helper Coroutines
    private IEnumerator ShowStepTextAndDelay(int instructionIndex, GamePhase phase, int narIndex = 0)
    {
        // [이동 금지] 설명 듣는 중
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetLocomotion(false);

        if (uiManager) { uiManager.CloseFeedBack(); uiManager.UpdateInstruction(instructionIndex); uiManager.OpenInstructionPanel(); }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNAR(GameScene.Simulator, phase, narIndex);

        float timer = 0f;
        while (uiManager.GetDisplayPanel() && timer < instructionDuration) { timer += Time.deltaTime; yield return null; }
        if (uiManager && uiManager.GetDisplayPanel()) uiManager.CloseInstructionPanel();

        // 여기서는 이동을 켜지 않음 (미션 시작 직전에 킴)
    }

    private IEnumerator ShowFeedbackAndDelay(int feedbackIndex, GamePhase phase, bool isNegative = false, int narIndex = 1)
    {
        // [이동 금지] 피드백 듣는 중
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetLocomotion(false);

        if (uiManager) { uiManager.CloseInstruction(); if (!isNegative) uiManager.UpdateFeedBack(feedbackIndex); else uiManager.UpdateNegativeFeedback(feedbackIndex); uiManager.OpenInstructionPanel(); }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNAR(GameScene.Simulator, phase, narIndex);
        if (AudioManager.Instance != null) { SFXType sfx = isNegative ? SFXType.Fail_Feedback : SFXType.Success_Feedback; AudioManager.Instance.PlaySFX(sfx); }
        if (!isNegative) TriggerHaptic(0.8f, 0.3f); else TriggerHaptic(0.4f, 0.1f);

        float timer = 0f; while (uiManager.GetDisplayPanel() && timer < feedbackDuration) { timer += Time.deltaTime; yield return null; }
        if (uiManager && uiManager.GetDisplayPanel()) uiManager.CloseInstructionPanel();
    }

    private IEnumerator ShowTimedMission(string missionText, System.Func<bool> missionCondition, System.Func<float> progressCalculator = null, bool isDisplayPanel = false)
    {
        // [이동 허용] 미션 시작 시점
        // 단, '자세 취하기'나 '기둥 잡기' 같은 정지 미션에서는 기획에 따라 꺼야 할 수도 있음.
        // 여기서는 기본적으로 미션 중에는 이동 가능하게 설정 (필요 시 시나리오에서 개별 제어)
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetLocomotion(true);

        if (uiManager) yield return uiManager.StartCoroutine(uiManager.StartMissionTimer(missionText, phaseTime, missionCondition, progressCalculator, isDisplayPanel));
        else yield return new WaitUntil(missionCondition);
    }

    // ... MonitorContinuousAction, ReturnToSavedPositionRoutine 등 기존 유지 ...
    private IEnumerator MonitorContinuousAction(System.Func<bool> actionCondition, float requiredDuration)
    {
        isActionCompleted = false; currentActionHoldTimer = 0f;
        if (requiredDuration > 0f)
        {
            while (!isActionCompleted)
            {
                if (actionCondition.Invoke()) { currentActionHoldTimer += Time.deltaTime; if (Time.frameCount % 10 == 0) TriggerHaptic(0.1f, 0.05f); }
                else { currentActionHoldTimer -= Time.deltaTime * 2.0f; }
                currentActionHoldTimer = Mathf.Clamp(currentActionHoldTimer, 0f, requiredDuration);
                if (currentActionHoldTimer >= requiredDuration) { isActionCompleted = true; break; }
                yield return null;
            }
        }
    }

    private IEnumerator ReturnToSavedPositionRoutine()
    {
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetLocomotion(false);
        yield return StartCoroutine(ShowFeedbackAndDelay(0, GamePhase.Move1, true, 2));
        if (PlayerTransform != null && startPosition != Vector3.zero) PlayerTransform.position = startPosition;
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetLocomotion(true);
    }
    #endregion

    #region Main Scenario Coroutine
    private IEnumerator ScenarioRoutine()
    {
        DataManager.Instance.InitializeSessionData();

        // 1. 시작: 조작 불가
        if (PlayerManager.Instance != null) { PlayerManager.Instance.SetLocomotion(false); PlayerManager.Instance.SetInteraction(false); }

        // Intro
        currentPhase = GamePhase.Caution;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayAMB(AMBType.Crowd, 0);
        if (uiManager) { uiManager.SetDisplayPanel(true); uiManager.OpenCautionPanel(); }
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel()); yield return new WaitForSeconds(nextStepDuration);

        if (uiManager) { uiManager.SetDisplayPanel(true); uiManager.OpenSituationPanel(); }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNAR(GameScene.Simulator, GamePhase.Caution, 0);
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel()); yield return new WaitForSeconds(nextStepDuration);

        // 상호작용 허용
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetInteraction(true);

        // Phase 0: Tutorial
        currentPhase = GamePhase.Tutorial; UpdateAmbience(currentPhase);
        yield return StartCoroutine(ShowStepTextAndDelay(0, GamePhase.Tutorial));
        // -> 이동 불가 상태

        targetIndex = 0; SetZoneActive(targetIndex, true); if (uiManager) uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission("목표지점으로 이동", () => isZoneReached));
        // -> ShowTimedMission 내부에서 이동 허용됨

        SetZoneActive(targetIndex, false); isZoneReached = false;
        yield return StartCoroutine(ShowFeedbackAndDelay(0, GamePhase.Tutorial));
        // -> 이동 불가 상태
        yield return new WaitForSeconds(nextStepDuration);

        // Phase 1: Move1
        currentPhase = GamePhase.Move1; UpdateAmbience(currentPhase);

        // [예외] 여기는 바로 이동 미션 전 설명 없이 이동해야 할 수도 있음.
        // 하지만 시나리오상 ShowStepTextAndDelay가 뒤에 나오므로, 일단 이동 허용
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetLocomotion(true);
        targetIndex = 1; SetZoneActive(targetIndex, true); yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false); isZoneReached = false;

        if (uiManager) { uiManager.UpdatePressureGauge(1); uiManager.OpenPressurePanel(); }
        SavePlayerPosition();

        yield return StartCoroutine(ShowStepTextAndDelay(1, GamePhase.Move1));
        // -> 이동 불가

        targetIndex = 2; SetZoneActive(targetIndex, true); if (uiManager) uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission("대각선으로 이동", () => isZoneReached));
        // -> 이동 허용

        SetZoneActive(targetIndex, false); isZoneReached = false;
        if (uiManager) uiManager.UpdatePressureGauge(0);
        yield return StartCoroutine(ShowFeedbackAndDelay(1, GamePhase.Move1));
        yield return new WaitForSeconds(nextStepDuration);

        // Phase 2: ABCPose (자세 취하기)
        currentPhase = GamePhase.ABCPose; UpdateAmbience(currentPhase);
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetLocomotion(true);

        targetIndex = 3; SetZoneActive(targetIndex, true); yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false); isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(2);
        yield return StartCoroutine(ShowStepTextAndDelay(2, GamePhase.ABCPose));

        // 자세 취하기 미션
        Coroutine monitorCoroutine = StartCoroutine(MonitorContinuousAction(() => gestureManager.IsActionValid(), targetHoldTime));
        if (uiManager) uiManager.DisplayTipsImage(0);
        yield return StartCoroutine(ShowTimedMission("ABC 자세 취하기", () => isActionCompleted, () => currentActionHoldTimer / targetHoldTime, true));
        StopCoroutine(monitorCoroutine);

        if (uiManager) { uiManager.UpdatePressureGauge(1); uiManager.SetPressureIntensity(0.0f); }
        yield return StartCoroutine(ShowFeedbackAndDelay(2, GamePhase.ABCPose));
        isActionCompleted = false; yield return new WaitForSeconds(nextStepDuration);

        // Phase 3: Move2
        currentPhase = GamePhase.Move2; UpdateAmbience(currentPhase);
        if (uiManager) uiManager.UpdatePressureGauge(2); SavePlayerPosition();
        yield return StartCoroutine(ShowStepTextAndDelay(3, GamePhase.Move2));

        targetIndex = 4; SetZoneActive(targetIndex, true); if (uiManager) uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission("대각선으로 이동", () => isZoneReached));

        SetZoneActive(targetIndex, false); isZoneReached = false;
        if (uiManager) uiManager.UpdatePressureGauge(1);
        yield return StartCoroutine(ShowFeedbackAndDelay(3, GamePhase.Move2)); yield return new WaitForSeconds(nextStepDuration);

        // Phase 4: HoldPillar
        currentPhase = GamePhase.HoldPillar; UpdateAmbience(currentPhase);
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetLocomotion(true);

        targetIndex = 5; SetZoneActive(targetIndex, true); yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false); isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(3);
        yield return StartCoroutine(ShowStepTextAndDelay(4, GamePhase.HoldPillar));

        monitorCoroutine = StartCoroutine(MonitorContinuousAction(() => gestureManager.IsHoldingClimbHandle(), targetHoldTime));
        if (uiManager) uiManager.DisplayTipsImage(1);
        targetIndex = 6; SetZoneActive(targetIndex, true);
        yield return StartCoroutine(ShowTimedMission("기둥 잡기", () => isActionCompleted, () => currentActionHoldTimer / targetHoldTime, true));
        StopCoroutine(monitorCoroutine); SetZoneActive(targetIndex, false);

        if (uiManager) uiManager.UpdatePressureGauge(1);
        yield return StartCoroutine(ShowFeedbackAndDelay(4, GamePhase.HoldPillar));
        isActionCompleted = false; yield return new WaitForSeconds(nextStepDuration);

        // Phase 5: ClimbUp
        currentPhase = GamePhase.ClimbUp; UpdateAmbience(currentPhase);
        if (PlayerManager.Instance != null) PlayerManager.Instance.SetLocomotion(true);

        targetIndex = 7; SetZoneActive(targetIndex, true); yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false); isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(4);
        yield return StartCoroutine(ShowStepTextAndDelay(5, GamePhase.ClimbUp));

        targetIndex = 8; SetZoneActive(targetIndex, true);
        monitorCoroutine = StartCoroutine(MonitorContinuousAction(() => gestureManager.IsHoldingClimbHandle(), targetHoldTime));
        if (uiManager) uiManager.DisplayTipsImage(1);
        yield return StartCoroutine(ShowTimedMission("벽 잡기", () => isActionCompleted, () => currentActionHoldTimer / targetHoldTime, true));
        StopCoroutine(monitorCoroutine); SetZoneActive(targetIndex, false);

        if (uiManager) uiManager.UpdatePressureGauge(2);
        yield return StartCoroutine(ShowFeedbackAndDelay(5, GamePhase.ClimbUp));
        isZoneReached = false; isActionCompleted = false; yield return new WaitForSeconds(nextStepDuration);

        // Phase 6: Escape
        currentPhase = GamePhase.Escape; UpdateAmbience(currentPhase);
        if (uiManager) uiManager.UpdatePressureGauge(0);
        yield return StartCoroutine(ShowStepTextAndDelay(6, GamePhase.Escape));

        targetIndex = 9; SetZoneActive(targetIndex, true); if (uiManager) uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission("안전구역으로 이동", () => isZoneReached));

        SetZoneActive(targetIndex, false); isZoneReached = false;
        if (uiManager) { uiManager.UpdatePressureGauge(0); uiManager.ClosePressurePanel(); }

        // Finish
        if (PlayerManager.Instance != null) { PlayerManager.Instance.SetLocomotion(false); PlayerManager.Instance.SetInteraction(false); }
        currentPhase = GamePhase.Finished;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNAR(GameScene.Simulator, GamePhase.Finished, 0);
        if (GameManager.Instance != null) GameManager.Instance.TriggerGameClear();
        if (uiManager) uiManager.ShowOuttroUI();
    }

    private void SetZoneActive(int index, bool isActive) { if (TargerZone != null && TargerZone.Length > index && TargerZone[index] != null) TargerZone[index].SetActive(isActive); }
    private void SetInteractionLimit(bool isActive) { if (PlayerManager.Instance != null) { PlayerManager.Instance.SetLocomotion(!isActive); PlayerManager.Instance.SetInteraction(!isActive); } }
    #endregion
}