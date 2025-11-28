using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 게임의 전체 시나리오 흐름(튜토리얼 -> 이동 -> 액션 -> 탈출)을 제어하는 메인 매니저입니다.
/// [수정됨] 미션(ShowTimedMission) 중에만 이동이 가능하도록 로직 변경
/// </summary>
public class GameStepManager : MonoBehaviour
{

    [Header("Linked Managers")]
    [Tooltip("UI 제어를 담당하는 매니저")]
    [SerializeField] private IngameUIManager uiManager;
    [Tooltip("플레이어의 제스처 및 입력 판정을 담당하는 매니저")]
    [SerializeField] private GestureManager gestureManager;

    [Header("Settings - Action")]
    [Tooltip("액션(자세 유지, 잡기 등)을 유지해야 하는 목표 시간 (초)")]
    [SerializeField] private float targetHoldTime = 3.0f;

    [Header("Settings - Timing")]
    [Tooltip("각 페이즈(미션)의 제한 시간")]
    [SerializeField] private float phaseTime = 60.0f;
    [Tooltip("미션 시작 전 안내 텍스트가 표시되는 시간")]
    [SerializeField] private float instructionDuration = 5.0f;
    [Tooltip("미션 완료/실패 후 피드백 텍스트가 표시되는 시간")]
    [SerializeField] private float feedbackDuration = 5.0f;
    [Tooltip("다음 단계로 넘어가기 전 대기 시간")]
    [SerializeField] private float nextStepDuration = 1.0f;

    [Header("Zone Objects")]
    [Tooltip("각 단계에서 활성화될 목표 지점 0:Tutorial, 1:Move1, 2:Move2, 3:Escape")]
    [SerializeField] private GameObject[] TargerZone;
    private int targetIndex;

    [SerializeField] private enum GamePhase { Caution, Tutorial, Move1, ABCPose, Move2, HoldPillar, ClimbUp, Escape, Finished }
    


    [Header("Debug Info")]
    [SerializeField] private GamePhase currentPhase;

    // =================================================================================
    // 내부 상태 변수
    // =================================================================================
    private bool isZoneReached = false;
    private float currentActionHoldTimer = 0f;
    private bool isActionCompleted = false;

    // =================================================================================
    // Unity 생명 주기 메서드
    // =================================================================================

    private void Start()
    {
        StartCoroutine(ScenarioRoutine());
    }

    public void SetZoneReached(bool reached)
    {
        isZoneReached = reached;
    }

    #region UI Helper Coroutines (UI 제어 보조 코루틴) - [이동 제어 코드 삭제됨]

    /// <summary>
    /// 미션 안내 텍스트를 띄우고 일정 시간 대기하는 코루틴
    /// </summary>
    private IEnumerator ShowStepTextAndDelay(string instructionText, string missionText)
    {

        // 1. UI 설정 및 표시
        if (uiManager)
        {
            uiManager.UpdateFeedBack("");
            uiManager.UpdateInstruction(instructionText);
            uiManager.UpdateMission(missionText);
            uiManager.OpenInstructionPanel();
        }

        // 2. 대기
        float timer = 0f;
        while (uiManager.GetDisplayPanel() && timer < instructionDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 3. 패널 닫기
        if (uiManager && uiManager.GetDisplayPanel()) uiManager.CloseInstructionPanel();
    }

    /// <summary>
    /// 결과 피드백 텍스트를 띄우고 일정 시간 대기하는 코루틴
    /// </summary>
    private IEnumerator ShowFeedbackAndDelay(string feedbackText)
    {
        // [삭제] 이동 제어 코드 제거됨

        // 1. UI 설정
        if (uiManager)
        {
            uiManager.UpdateInstruction("");
            uiManager.UpdateMission("");
            uiManager.UpdateFeedBack(feedbackText);
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
        if (uiManager && uiManager.GetDisplayPanel()) uiManager.CloseInstructionPanel();

        // [삭제] 이동 재활성화 코드 제거됨
    }

    /// <summary>
    /// 제한 시간이 있는 미션을 실행하는 코루틴
    /// </summary>
    private IEnumerator ShowTimedMission(string missionText, System.Func<bool> missionCondition, System.Func<float> progressCalculator = null)
    {
        // 미션 시작 -> 이동 허용!
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetLocomotion(true);
            PlayerManager.Instance.SetInteraction(true);
        }

        // IngameUIManager의 타이머 코루틴 실행 및 대기
        Coroutine timerCoroutine = uiManager.StartCoroutine(uiManager.StartMissionTimer(
            missionText,
            phaseTime,
            missionCondition,
            progressCalculator
        ));

        yield return timerCoroutine;

        // 미션 종료 -> 이동 잠금!
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetLocomotion(false);
            PlayerManager.Instance.SetInteraction(false);
        }
    }

    #endregion

    #region Logic Helper Coroutines

    private IEnumerator MonitorContinuousAction(System.Func<bool> actionCondition, float requiredDuration)
    {
        isActionCompleted = false;
        currentActionHoldTimer = 0f;

        if (requiredDuration > 0f)
        {
            while (!isActionCompleted)
            {
                if (actionCondition.Invoke())
                {
                    currentActionHoldTimer += Time.deltaTime;
                }
                else
                {
                    currentActionHoldTimer -= Time.deltaTime * 2.0f;
                }

                currentActionHoldTimer = Mathf.Clamp(currentActionHoldTimer, 0f, requiredDuration);

                if (currentActionHoldTimer >= requiredDuration)
                {
                    isActionCompleted = true;
                    break;
                }
                yield return null;
            }
        }
    }

    #endregion

    /// <summary>
    /// 전체 게임 시나리오 메인 코루틴
    /// </summary>
    private IEnumerator ScenarioRoutine()
    {
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetLocomotion(false);
            PlayerManager.Instance.SetInteraction(false);
        }

        // ---------------------------------------------------------------------------------
        // Intro: 주의사항 패널
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Caution;
        Debug.Log("Caution Start");

        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.OpenCautionPanel();
        }
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel());

        Debug.Log("Caution Close");
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 0: 튜토리얼
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Tutorial;
        // 텍스트 출력 중에는 이동 불가 (기본 상태)
        yield return StartCoroutine(ShowStepTextAndDelay(
            "튜토리얼: 바닥의 화살표를 따라 목표 지점으로 이동하세요.", " ")
        );

        targetIndex = 0;
        if (TargerZone.Length > targetIndex) TargerZone[targetIndex].SetActive(true);

        uiManager.DisplayTipsImage(0);
        // 미션 시작 -> 이동 가능 (ShowTimedMission 내부에서 처리)
        yield return StartCoroutine(ShowTimedMission(
            "목표지점으로 이동",
            () => isZoneReached
        ));
        // 미션 종료 -> 이동 불가

        isZoneReached = false;
        yield return StartCoroutine(ShowFeedbackAndDelay("튜토리얼을 완수하셨습니다!"));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 1: 1차 대각선 이동
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Move1;
        uiManager.UpdatePressureGauge(3);
        uiManager.OpenPressurePanel();
        yield return StartCoroutine(ShowStepTextAndDelay(
            "행사로 인해 거리에 인파가 몰리고 있습니다.\n이동 속도가 느려지면 탈출해야 합니다.",
            "사람이 많은 곳은 피해서, 가장자리로 계속 이동하세요.")
        );

        targetIndex = 1;
        if (TargerZone.Length > targetIndex) TargerZone[targetIndex].SetActive(true);

        uiManager.DisplayTipsImage(0);
        yield return StartCoroutine(ShowTimedMission(
            "대각선으로 이동",
            () => isZoneReached
        ));
        isZoneReached = false;

        uiManager.UpdatePressureGauge(2);
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다. 가장자리는 비교적 안전합니다."));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 2: ABC 자세
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.ABCPose;
        uiManager.UpdatePressureGauge(4);
        yield return StartCoroutine(ShowStepTextAndDelay(
            "압박이 느껴지고 있습니다.\n다리는 어깨너비로, 팔은 가슴 앞 공간을 확보하세요.",
            "가슴 공간 확보 자세(ABC 자세)를 취하세요.")
        );

        Coroutine monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsActionValid(),
            targetHoldTime
        ));

        uiManager.DisplayTipsImage(1);
        // 이 미션 중에는 이동이 가능해지지만, 제자리에서 자세를 취해야 하므로 문제없음
        // (혹은 필요하다면 자세 취하기 단계에서는 SetLocomotion(false)를 강제할 수도 있음)
        yield return StartCoroutine(ShowTimedMission(
            "ABC 자세 취하기",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime
        ));

        StopCoroutine(monitorCoroutine);
        uiManager.UpdatePressureGauge(3);
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다, 압박이 조금 완화되었습니다."));

        isActionCompleted = false;
        if (uiManager) uiManager.SetPressureIntensity(0.0f);
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 3: 2차 대각선 이동
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Move2;
        uiManager.UpdatePressureGauge(4);
        yield return StartCoroutine(ShowStepTextAndDelay(
            "다시 인파가 몰리고 있습니다.\n즉시 탈출해야 합니다.",
            "사람이 많은 곳은 피해서, 가장자리로 계속 이동하세요.")
        );

        targetIndex = 2;
        if (TargerZone.Length > targetIndex) TargerZone[targetIndex].SetActive(true);

        uiManager.DisplayTipsImage(0);
        yield return StartCoroutine(ShowTimedMission(
            "대각선으로 이동",
            () => isZoneReached
        ));
        isZoneReached = false;

        uiManager.UpdatePressureGauge(3);
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다. 가장자리는 비교적 안전합니다."));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 4: 기둥 잡기
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.HoldPillar;
        uiManager.UpdatePressureGauge(4);
        yield return StartCoroutine(ShowStepTextAndDelay(
            "사람들이 넘어지고 있습니다.\n중심을 잃지 않도록 가까운 기둥을 잡으세요.",
            "기둥을 Grab 버튼으로 잡고 3초 이상 유지하세요.")
        );

        monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsHoldingClimbHandle(),
            targetHoldTime
        ));

        uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission(
            "기둥 잡기",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime
        ));

        StopCoroutine(monitorCoroutine);
        uiManager.UpdatePressureGauge(3);
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다. 중심을 유지했습니다."));

        isActionCompleted = false;
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 5: 구조물 오르기
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.ClimbUp;
        uiManager.UpdatePressureGauge(4);
        yield return StartCoroutine(ShowStepTextAndDelay(
            "탈출로에 가까워질수록 인파가 밀집되고 있습니다.\n구조물을 이용해 잠시 숨을 확보하세요.",
            "벽을 타고 올라가 3초 이상 유지하세요.")
        );

        monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsHoldingClimbHandle(),
            3.0f
        ));

        uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission(
            "구조물 오르기",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime
        ));

        StopCoroutine(monitorCoroutine);
        isZoneReached = false;
        isActionCompleted = false;

        uiManager.UpdatePressureGauge(3);
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다, 지금은 잠시 안전합니다. 숨을 고르세요."));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 6: 최종 탈출
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Escape;
        uiManager.UpdatePressureGauge(2);
        yield return StartCoroutine(ShowStepTextAndDelay(
            "인파가 밀집 공간에서 벗어났습니다.\n이제 안전한 곳으로 이동하세요.",
            "경찰/구조대가 있는 안전 구역으로 이동하세요.")
        );

        targetIndex = 3;
        if (TargerZone.Length > targetIndex) TargerZone[targetIndex].SetActive(true);

        uiManager.DisplayTipsImage(0);
        yield return StartCoroutine(ShowTimedMission(
            "안전구역으로 이동",
            () => isZoneReached
        ));
        isZoneReached = false;

        uiManager.UpdatePressureGauge(0);
        uiManager.ClosePressurePanel();

        // ---------------------------------------------------------------------------------
        // Phase 7: 게임 종료
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Finished;

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameClear();

        if (uiManager) uiManager.ShowOuttroUI();
    }
}