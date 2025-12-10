using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 게임의 전체 시나리오 흐름(튜토리얼 -> 이동 -> 액션 -> 탈출)을 순차적으로 제어하는 메인 매니저입니다.
/// <para>
/// 1. 각 게임 페이즈(Phase)별로 미션을 부여하고 성공/실패를 판정합니다.<br/>
/// 2. PlayerManager를 통해 이동(Locomotion) 권한을 미션 중에만 부여합니다.<br/>
/// 3. IngameUIManager와 연동하여 안내 텍스트, 타이머, 피드백을 표시합니다.
/// </para>
/// </summary>
public class GameStepManager : MonoBehaviour
{
    #region Inspector Settings (References)

    [Header("Player References")]
    [Tooltip("플레이어의 Transform (위치 리셋용)")]
    [SerializeField] private Transform PlayerTransform;

    [Header("Linked Managers")]
    [Tooltip("인게임 UI 제어를 담당하는 매니저")]
    [SerializeField] private IngameUIManager uiManager;

    [Tooltip("플레이어의 제스처 및 입력 판정을 담당하는 매니저")]
    [SerializeField] private GestureManager gestureManager;

    [Header("Zone Objects")]
    [Tooltip("각 단계에서 활성화될 목표 지점 트리거 오브젝트들 (0:Tutorial, 1:Move1, 2:Move2, 3:Escape)")]
    [SerializeField] private GameObject[] TargerZone;

    #endregion

    #region Inspector Settings (Game Logic)

    [Header("Action Settings")]
    [Tooltip("액션(자세 유지, 잡기 등)을 성공하기 위해 유지해야 하는 목표 시간 (초)")]
    [SerializeField] private float targetHoldTime = 3.0f;

    [Header("Timing Settings")]
    [Tooltip("각 페이즈(미션)의 제한 시간")]
    [SerializeField] private float phaseTime = 60.0f;

    [Tooltip("미션 시작 전 안내 텍스트가 표시되는 시간")]
    [SerializeField] private float instructionDuration = 5.0f;

    [Tooltip("미션 완료/실패 후 피드백 텍스트가 표시되는 시간")]
    [SerializeField] private float feedbackDuration = 5.0f;

    [Tooltip("다음 단계로 넘어가기 전 대기 시간")]
    [SerializeField] private float nextStepDuration = 1.0f;

    #endregion

    #region Internal State & Debug

    private enum GamePhase
    {
        Caution, Tutorial, Move1, ABCPose, Move2, HoldPillar, ClimbUp, Escape, Finished
    }

    [Header("Debug Info")]
    [SerializeField] private GamePhase currentPhase;

    // 내부 상태 변수
    private bool isZoneReached = false;        // 목표 지점 도달 여부
    private bool isActionCompleted = false;    // 액션(자세/잡기) 완료 여부
    private float currentActionHoldTimer = 0f; // 현재 액션 유지 시간
    private int targetIndex;                   // 현재 목표 구역 인덱스
    private Vector3 startPosition;             // 위치 리셋을 위한 저장된 위치

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // 게임 시작 시 시나리오 코루틴 가동
        StartCoroutine(ScenarioRoutine());
    }

    #endregion

    #region Public API

    /// <summary>
    /// ZoneTrigger에서 호출하여 목표 지점에 도달했음을 알립니다.
    /// </summary>
    public void SetZoneReached(bool reached)
    {
        isZoneReached = reached;
    }

    /// <summary>
    /// 현재 플레이어의 위치를 저장합니다. (위험 구간 진입 전 체크포인트)
    /// </summary>
    public void SavePlayerPosition()
    {
        if (PlayerTransform != null)
        {
            startPosition = PlayerTransform.position;
            Debug.Log($"[GameStepManager] Player position saved: {startPosition}");
        }
    }

    /// <summary>
    /// 플레이어를 저장된 위치로 되돌리고 경고 피드백을 표시합니다.
    /// </summary>
    public void ReturnToSavedPosition()
    {
        // 중복 실행 방지를 위해 기존 코루틴 정지 후 재실행
        StopCoroutine(ReturnToSavedPositionRoutine());
        StartCoroutine(ReturnToSavedPositionRoutine());
    }

    #endregion

    #region UI & Logic Helper Coroutines

    /// <summary>
    /// 미션 안내 텍스트를 화면에 띄우고 일정 시간 대기합니다.
    /// </summary>
    private IEnumerator ShowStepTextAndDelay(int instructionText)
    {
        SetInteractionLimit(true);

        // 1. UI 설정 및 표시
        if (uiManager)
        {
            uiManager.CloseFeedBack();
            uiManager.UpdateInstruction(instructionText);
            uiManager.OpenInstructionPanel();
        }

        // 2. 지정된 시간만큼 대기
        float timer = 0f;
        while (uiManager.GetDisplayPanel() && timer < instructionDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 3. 패널 닫기
        if (uiManager && uiManager.GetDisplayPanel())
            uiManager.CloseInstructionPanel();

        SetInteractionLimit(false);
    }

    /// <summary>
    /// 결과 피드백 텍스트를 띄우고 일정 시간 대기합니다.
    /// </summary>
    private IEnumerator ShowFeedbackAndDelay(int feedbackText, bool isNegative = false)
    {
        SetInteractionLimit(true);

        // 1. UI 설정
        if (uiManager)
        {
            uiManager.CloseInstruction();
            if(!isNegative) uiManager.UpdateFeedBack(feedbackText);
            else uiManager.UpdateNegativeFeedback(feedbackText);
            uiManager.OpenInstructionPanel();
        }

        // 2. 대기
        float timer = 0f;
        while (uiManager.GetDisplayPanel() && timer < feedbackDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 3. 패널 닫기
        if (uiManager && uiManager.GetDisplayPanel())
            uiManager.CloseInstructionPanel();

        SetInteractionLimit(false);
    }

    /// <summary>
    /// 제한 시간이 있는 미션을 실행합니다. 미션 중에만 이동(Locomotion)이 허용됩니다.
    /// </summary>
    private IEnumerator ShowTimedMission(string missionText, System.Func<bool> missionCondition, System.Func<float> progressCalculator = null, bool isDisplayPanel = false)
    {
        // [핵심 로직] 미션 시작 -> 이동 허용
        /*
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetLocomotion(true);
            PlayerManager.Instance.SetInteraction(true);
        }
        */

        // UI 매니저의 타이머 코루틴 실행 (조건 달성 시까지 대기)
        if (uiManager)
        {
            yield return uiManager.StartCoroutine(uiManager.StartMissionTimer(
                missionText,
                phaseTime,
                missionCondition,
                progressCalculator,
                isDisplayPanel
            ));
        }
        else
        {
            // UI 매니저가 없을 경우를 대비한 안전장치 (조건만 기다림)
            yield return new WaitUntil(missionCondition);
        }

        // [핵심 로직] 미션 종료 -> 이동 잠금
        /*
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetLocomotion(false);
            PlayerManager.Instance.SetInteraction(false);
        }
        */
    }

    /// <summary>
    /// 특정 액션(조건)이 일정 시간 동안 지속되는지 감시합니다.
    /// </summary>
    private IEnumerator MonitorContinuousAction(System.Func<bool> actionCondition, float requiredDuration)
    {
        isActionCompleted = false;
        currentActionHoldTimer = 0f;

        if (requiredDuration > 0f)
        {
            while (!isActionCompleted)
            {
                // 조건을 만족하면 타이머 증가, 아니면 감소(빠르게)
                if (actionCondition.Invoke())
                {
                    currentActionHoldTimer += Time.deltaTime;
                }
                else
                {
                    currentActionHoldTimer -= Time.deltaTime * 2.0f;
                }

                currentActionHoldTimer = Mathf.Clamp(currentActionHoldTimer, 0f, requiredDuration);

                // 목표 시간 도달 시 완료 처리
                if (currentActionHoldTimer >= requiredDuration)
                {
                    isActionCompleted = true;
                    break;
                }
                yield return null;
            }
        }
    }

    /// <summary>
    /// 플레이어 위치 리셋 및 경고 표시를 처리하는 코루틴
    /// </summary>
    private IEnumerator ReturnToSavedPositionRoutine()
    {
        // 1. 이동 잠금
        
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(false);
        

        // 2. 경고 피드백 표시
        yield return StartCoroutine(ShowFeedbackAndDelay(0,true));

        // 3. 위치 이동
        if (PlayerTransform != null && startPosition != Vector3.zero)
        {
            PlayerTransform.position = startPosition;
            Debug.Log($"[GameStepManager] Player returned to saved position: {startPosition}");
        }
        else
        {
            Debug.LogWarning("[GameStepManager] Cannot return to saved position (Invalid data).");
        }

        // 4. 이동 재활성화 (미션 중이므로 다시 켜줌)
        
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(true);
        
    }

    #endregion

    #region Main Scenario Coroutine

    /// <summary>
    /// 게임의 전체 시나리오를 단계별로 실행하는 메인 코루틴입니다.
    /// </summary>
    private IEnumerator ScenarioRoutine()
    {
        // 0. 초기화
        DataManager.Instance.InitializeSessionData();

        SetInteractionLimit(true);

        // ---------------------------------------------------------------------------------
        // Intro: 주의사항 패널
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Caution;
        Debug.Log("[Scenario] Caution Phase Start");

        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.OpenCautionPanel();
        }

        // 패널이 닫힐 때까지 대기
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel());

        yield return new WaitForSeconds(nextStepDuration);

        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.OpenSituationPanel();
        }

        // 패널이 닫힐 때까지 대기
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel());

        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 0: 튜토리얼 (기본 이동)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Tutorial;

        yield return StartCoroutine(ShowStepTextAndDelay(0));

        targetIndex = 0;
        SetZoneActive(targetIndex, true);
        if (uiManager) uiManager.DisplayTipsImage(2);

        SetInteractionLimit(false);

        // 미션 실행 (이동 가능)
        yield return StartCoroutine(ShowTimedMission(
            "목표지점으로 이동",
            () => isZoneReached
        ));

        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        yield return StartCoroutine(ShowFeedbackAndDelay(0));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 1: 1차 이동 (대각선 / 가장자리)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Move1;

        targetIndex = 1;
        SetZoneActive(targetIndex, true);
        yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager)
        {
            uiManager.UpdatePressureGauge(1);
            uiManager.OpenPressurePanel();
        }
        SavePlayerPosition();

        yield return StartCoroutine(ShowStepTextAndDelay(1));

        targetIndex = 2;
        SetZoneActive(targetIndex, true);
        if (uiManager) uiManager.DisplayTipsImage(2);

        yield return StartCoroutine(ShowTimedMission(
            "대각선으로 이동",
            () => isZoneReached
        ));

        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(0);
        yield return StartCoroutine(ShowFeedbackAndDelay(1));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 2: ABC 자세 (가슴 압박 방지)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.ABCPose;

        targetIndex = 3;
        SetZoneActive(targetIndex, true);
        yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(2);

        yield return StartCoroutine(ShowStepTextAndDelay(2));

        // 자세 감지 모니터링 시작
        Coroutine monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsActionValid(),
            targetHoldTime
        ));

        if (uiManager) uiManager.DisplayTipsImage(0);

        yield return StartCoroutine(ShowTimedMission(
            "ABC 자세 취하기",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime,
            true
        ));

        StopCoroutine(monitorCoroutine);

        if (uiManager)
        {
            uiManager.UpdatePressureGauge(1);
            uiManager.SetPressureIntensity(0.0f);
        }

        yield return StartCoroutine(ShowFeedbackAndDelay(2));

        isActionCompleted = false;
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 3: 2차 이동 (탈출 지속)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Move2;

        if (uiManager) uiManager.UpdatePressureGauge(2);
        SavePlayerPosition();

        yield return StartCoroutine(ShowStepTextAndDelay(3));

        targetIndex = 4;
        SetZoneActive(targetIndex, true);
        if (uiManager) uiManager.DisplayTipsImage(2);

        yield return StartCoroutine(ShowTimedMission(
            "대각선으로 이동",
            () => isZoneReached
        ));

        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(1);
        yield return StartCoroutine(ShowFeedbackAndDelay(3));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 4: 기둥 잡기 (넘어짐 방지)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.HoldPillar;

        targetIndex = 5;
        SetZoneActive(targetIndex, true);
        yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(3);

        yield return StartCoroutine(ShowStepTextAndDelay(4));

        monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsHoldingClimbHandle(),
            targetHoldTime
        ));

        if (uiManager) uiManager.DisplayTipsImage(1);

        targetIndex = 6;
        SetZoneActive(targetIndex, true);

        yield return StartCoroutine(ShowTimedMission(
            "기둥 잡기",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime,
            true
        ));

        StopCoroutine(monitorCoroutine);

        SetZoneActive(targetIndex, false);

        if (uiManager) uiManager.UpdatePressureGauge(1);
        yield return StartCoroutine(ShowFeedbackAndDelay(4));

        isActionCompleted = false;
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 5: 벽잡기 (숨 확보)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.ClimbUp;

        targetIndex = 7;
        SetZoneActive(targetIndex, true);
        yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(4);

        yield return StartCoroutine(ShowStepTextAndDelay(5));

        targetIndex = 8;
        SetZoneActive(targetIndex, true);

        // 오르기 판정도 HoldPillar와 유사하게 ClimbHandle을 잡고 있는 것으로 판단
        monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsHoldingClimbHandle(),
            targetHoldTime
        ));

        if (uiManager) uiManager.DisplayTipsImage(1);

        yield return StartCoroutine(ShowTimedMission(
            "벽 잡기",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime,
            true
        ));

        StopCoroutine(monitorCoroutine);

        SetZoneActive(targetIndex, false);

        if (uiManager) uiManager.UpdatePressureGauge(2);
        yield return StartCoroutine(ShowFeedbackAndDelay(5));

        isZoneReached = false;
        isActionCompleted = false;
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 6: 최종 탈출
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Escape;
        if (uiManager) uiManager.UpdatePressureGauge(0);

        yield return StartCoroutine(ShowStepTextAndDelay(6));

        targetIndex = 9;
        SetZoneActive(targetIndex, true);
        if (uiManager) uiManager.DisplayTipsImage(2);

        yield return StartCoroutine(ShowTimedMission(
            "안전구역으로 이동",
            () => isZoneReached
        ));

        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager)
        {
            uiManager.UpdatePressureGauge(0);
            uiManager.ClosePressurePanel();
        }

        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetLocomotion(false);
            PlayerManager.Instance.SetInteraction(false);
        }

        // ---------------------------------------------------------------------------------
        // Phase 7: 게임 종료
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Finished;
        Debug.Log("[Scenario] Game Finished");

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameClear();

        if (uiManager) uiManager.ShowOuttroUI();
    }

    /// <summary>
    /// 목표 구역 오브젝트를 활성화/비활성화하는 헬퍼 메서드
    /// </summary>
    private void SetZoneActive(int index, bool isActive)
    {
        if (TargerZone != null && TargerZone.Length > index && TargerZone[index] != null)
        {
            TargerZone[index].SetActive(isActive);
        }
    }

    private void SetInteractionLimit(bool isActive)
    {
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetLocomotion(!isActive);
            PlayerManager.Instance.SetInteraction(!isActive);
        }
    }

    #endregion
}