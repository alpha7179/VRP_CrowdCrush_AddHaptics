using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;
using static GameManager;
using static AudioManager;

public class GameStepManager : MonoBehaviour
{
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
        StartCoroutine(ScenarioRoutine());
    }
    #endregion

    #region Public API
    public void SetZoneReached(bool reached) { isZoneReached = reached; }
    public void SavePlayerPosition() { if (PlayerTransform != null) startPosition = PlayerTransform.position; }
    public void ReturnToSavedPosition() { StopCoroutine(ReturnToSavedPositionRoutine()); StartCoroutine(ReturnToSavedPositionRoutine()); }
    #endregion

    #region Haptic & Audio Helpers
    private void TriggerHaptic(float rawAmplitude, float duration)
    {
        float finalAmplitude = rawAmplitude;
        if (DataManager.Instance != null)
        {
            finalAmplitude = DataManager.Instance.GetAdjustedHapticStrength(rawAmplitude);
        }
        if (finalAmplitude <= 0.01f) return;

        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        foreach (var device in devices)
        {
            if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
            {
                device.SendHapticImpulse(0, finalAmplitude, duration);
            }
        }
    }

    private void UpdateAmbience(GamePhase phase)
    {
        if (AudioManager.Instance == null) return;

        // 1. 군중 소리 (AMB 채널 - 깔리는 배경음)
        // 페이즈에 따라 군중 소리 크기/밀집도 변경
        switch (phase)
        {
            case GamePhase.Tutorial:
            case GamePhase.Move1:
                AudioManager.Instance.PlayAMB(AMBType.Crowd, 0);
                break;
            case GamePhase.ABCPose:
            case GamePhase.Move2:
            case GamePhase.HoldPillar:
                AudioManager.Instance.PlayAMB(AMBType.Crowd, 1);
                break;
            case GamePhase.ClimbUp:
            case GamePhase.Escape:
                AudioManager.Instance.PlayAMB(AMBType.Crowd, 2);
                break;
        }

        // 2. 경찰/앰뷸런스/이명 소리 (SFX Layer - 겹치는 소리 & 점점 커짐)
        // Move2 단계부터 시작
        if (phase >= GamePhase.Move2 && phase != GamePhase.Finished)
        {
            // (1) 소리 재생 (아직 안 켜져 있으면 켬)
            // 경찰: 셔플 재생 / 앰뷸런스: 단일 루프 / 이명: 단일 루프
            AudioManager.Instance.PlayShuffleSFX(SFXType.Police, true);
            AudioManager.Instance.PlaySFX(SFXType.Ambulance, true, true);
            AudioManager.Instance.PlaySFX(SFXType.EarRinging, true, true);

            // (2) 볼륨 스케일 계산 (점점 크게)
            float policeVol = 0.3f;
            float ambulanceVol = 0.3f;
            float ringingVol = 0.3f;

            if (phase == GamePhase.Move2)
            {
                policeVol = 0.4f; ambulanceVol = 0.4f; ringingVol = 0.2f;
            }
            else if (phase == GamePhase.HoldPillar)
            {
                policeVol = 0.6f; ambulanceVol = 0.6f; ringingVol = 0.4f;
            }
            else if (phase == GamePhase.ClimbUp)
            {
                policeVol = 0.8f; ambulanceVol = 0.8f; ringingVol = 0.6f;
            }
            else if (phase == GamePhase.Escape)
            {
                policeVol = 1.0f; ambulanceVol = 1.0f; ringingVol = 0.8f;
            }
            
            // (3) 볼륨 적용 
            AudioManager.Instance.SetLoopingSFXScale(SFXType.Police, policeVol);
            AudioManager.Instance.SetLoopingSFXScale(SFXType.Ambulance, ambulanceVol);
            AudioManager.Instance.SetLoopingSFXScale(SFXType.EarRinging, ringingVol);
        }
        else
        {
            // 해당 단계가 아니면 소리 끄기 (선택 사항 - 여기서는 유지하다가 Finish에서 끔)
            // if (phase < GamePhase.Move2) ... StopSFX...
        }
    }
    #endregion

    #region UI & Logic Helper Coroutines
    private IEnumerator ShowStepTextAndDelay(int instructionIndex, GamePhase phase, int narIndex = 0)
    {
        SetInteractionLimit(true);
        if (uiManager) { uiManager.CloseFeedBack(); uiManager.UpdateInstruction(instructionIndex); uiManager.OpenInstructionPanel(); }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNAR(GameScene.Simulator, phase, narIndex);
        float timer = 0f; while (uiManager.GetDisplayPanel() && timer < instructionDuration) { timer += Time.deltaTime; yield return null; }
        if (uiManager && uiManager.GetDisplayPanel()) uiManager.CloseInstructionPanel();
        SetInteractionLimit(false);
    }

    private IEnumerator ShowFeedbackAndDelay(int feedbackIndex, GamePhase phase, bool isNegative = false, int narIndex = 1)
    {
        SetInteractionLimit(true);
        if (uiManager) { uiManager.CloseInstruction(); if (!isNegative) uiManager.UpdateFeedBack(feedbackIndex); else uiManager.UpdateNegativeFeedback(feedbackIndex); uiManager.OpenInstructionPanel(); }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNAR(GameScene.Simulator, phase, narIndex);
        if (AudioManager.Instance != null) { SFXType sfx = isNegative ? SFXType.Fail_Feedback : SFXType.Success_Feedback; AudioManager.Instance.PlaySFX(sfx); }
        if (!isNegative) TriggerHaptic(0.8f, 0.3f); else TriggerHaptic(0.4f, 0.1f);
        float timer = 0f; while (uiManager.GetDisplayPanel() && timer < feedbackDuration) { timer += Time.deltaTime; yield return null; }
        if (uiManager && uiManager.GetDisplayPanel()) uiManager.CloseInstructionPanel();
        SetInteractionLimit(false);
    }

    private IEnumerator ShowTimedMission(string missionText, System.Func<bool> missionCondition, System.Func<float> progressCalculator = null, bool isDisplayPanel = false)
    {
        if (uiManager) yield return uiManager.StartCoroutine(uiManager.StartMissionTimer(missionText, phaseTime, missionCondition, progressCalculator, isDisplayPanel));
        else yield return new WaitUntil(missionCondition);
    }

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
        DataManager.Instance.InitializeSessionData(); SetInteractionLimit(true);

        // Intro
        currentPhase = GamePhase.Caution;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayAMB(AMBType.Crowd, 0);
        if (uiManager) { uiManager.SetDisplayPanel(true); uiManager.OpenCautionPanel(); }
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel()); yield return new WaitForSeconds(nextStepDuration);
        if (uiManager) { uiManager.SetDisplayPanel(true); uiManager.OpenSituationPanel(); }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNAR(GameScene.Simulator, GamePhase.Caution, 0);
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel()); yield return new WaitForSeconds(nextStepDuration);

        // Phase 0: Tutorial
        currentPhase = GamePhase.Tutorial; UpdateAmbience(currentPhase);
        yield return StartCoroutine(ShowStepTextAndDelay(0, GamePhase.Tutorial));
        targetIndex = 0; SetZoneActive(targetIndex, true); if (uiManager) uiManager.DisplayTipsImage(2);
        SetInteractionLimit(false);
        yield return StartCoroutine(ShowTimedMission("목표지점으로 이동", () => isZoneReached));
        SetZoneActive(targetIndex, false); isZoneReached = false;
        yield return StartCoroutine(ShowFeedbackAndDelay(0, GamePhase.Tutorial)); yield return new WaitForSeconds(nextStepDuration);

        // Phase 1: Move1
        currentPhase = GamePhase.Move1; UpdateAmbience(currentPhase);
        targetIndex = 1; SetZoneActive(targetIndex, true); yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false); isZoneReached = false;
        if (uiManager) { uiManager.UpdatePressureGauge(1); uiManager.OpenPressurePanel(); }
        SavePlayerPosition();
        yield return StartCoroutine(ShowStepTextAndDelay(1, GamePhase.Move1));
        targetIndex = 2; SetZoneActive(targetIndex, true); if (uiManager) uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission("대각선으로 이동", () => isZoneReached));
        SetZoneActive(targetIndex, false); isZoneReached = false;
        if (uiManager) uiManager.UpdatePressureGauge(0);
        yield return StartCoroutine(ShowFeedbackAndDelay(1, GamePhase.Move1)); yield return new WaitForSeconds(nextStepDuration);

        // Phase 2: ABCPose
        currentPhase = GamePhase.ABCPose; UpdateAmbience(currentPhase);
        targetIndex = 3; SetZoneActive(targetIndex, true); yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false); isZoneReached = false;
        if (uiManager) uiManager.UpdatePressureGauge(2);
        yield return StartCoroutine(ShowStepTextAndDelay(2, GamePhase.ABCPose));
        Coroutine monitorCoroutine = StartCoroutine(MonitorContinuousAction(() => gestureManager.IsActionValid(), targetHoldTime));
        if (uiManager) uiManager.DisplayTipsImage(0);
        yield return StartCoroutine(ShowTimedMission("ABC 자세 취하기", () => isActionCompleted, () => currentActionHoldTimer / targetHoldTime, true));
        StopCoroutine(monitorCoroutine);
        if (uiManager) { uiManager.UpdatePressureGauge(1); uiManager.SetPressureIntensity(0.0f); }
        yield return StartCoroutine(ShowFeedbackAndDelay(2, GamePhase.ABCPose)); isActionCompleted = false; yield return new WaitForSeconds(nextStepDuration);

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
        yield return StartCoroutine(ShowFeedbackAndDelay(4, GamePhase.HoldPillar)); isActionCompleted = false; yield return new WaitForSeconds(nextStepDuration);

        // Phase 5: ClimbUp
        currentPhase = GamePhase.ClimbUp; UpdateAmbience(currentPhase);
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
        yield return StartCoroutine(ShowFeedbackAndDelay(5, GamePhase.ClimbUp)); isZoneReached = false; isActionCompleted = false; yield return new WaitForSeconds(nextStepDuration);

        // Phase 6: Escape
        currentPhase = GamePhase.Escape; UpdateAmbience(currentPhase);
        if (uiManager) uiManager.UpdatePressureGauge(0);
        yield return StartCoroutine(ShowStepTextAndDelay(6, GamePhase.Escape));
        targetIndex = 9; SetZoneActive(targetIndex, true); if (uiManager) uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission("안전구역으로 이동", () => isZoneReached));
        SetZoneActive(targetIndex, false); isZoneReached = false;
        if (uiManager) { uiManager.UpdatePressureGauge(0); uiManager.ClosePressurePanel(); }
        if (PlayerManager.Instance != null) { PlayerManager.Instance.SetLocomotion(false); PlayerManager.Instance.SetInteraction(false); }

        // Finish
        currentPhase = GamePhase.Finished;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNAR(GameScene.Simulator, GamePhase.Finished, 0);
        if (GameManager.Instance != null) GameManager.Instance.TriggerGameClear();
        if (uiManager) uiManager.ShowOuttroUI();
    }

    private void SetZoneActive(int index, bool isActive) { if (TargerZone != null && TargerZone.Length > index && TargerZone[index] != null) TargerZone[index].SetActive(isActive); }
    private void SetInteractionLimit(bool isActive) { if (PlayerManager.Instance != null) { PlayerManager.Instance.SetLocomotion(!isActive); PlayerManager.Instance.SetInteraction(!isActive); } }
    #endregion
}