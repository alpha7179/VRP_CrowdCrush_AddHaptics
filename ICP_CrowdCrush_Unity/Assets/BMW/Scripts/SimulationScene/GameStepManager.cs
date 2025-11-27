using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 게임의 전체 시나리오 흐름(튜토리얼 -> 이동 -> 액션 -> 탈출)을 제어하는 메인 매니저입니다.
/// </summary>
public class GameStepManager : MonoBehaviour
{
    // =================================================================================
    // Inspector Field (직렬화된 변수)
    // =================================================================================

    [Header("Linked Managers")]
    [Tooltip("UI 제어를 담당하는 매니저")]
    [SerializeField] private IngameUIManager uiManager;
    [Tooltip("플레이어의 제스처 및 입력 판정을 담당하는 매니저")]
    [SerializeField] private GestureManager gestureManager; // IsActionValid, IsHoldingClimbHandle 등을 제공

    [Header("Settings - Action")]
    [Tooltip("액션(자세 유지, 잡기 등)을 유지해야 하는 목표 시간 (초)")]
    [SerializeField] private float targetHoldTime = 3.0f;

    [Header("Settings - Timing")]
    [Tooltip("각 페이즈(미션)의 제한 시간")]
    [SerializeField] private float phaseTime = 60.0f; // 최대 미션 수행 시간
    [Tooltip("미션 시작 전 안내 텍스트가 표시되는 시간")]
    [SerializeField] private float instructionDuration = 5.0f;
    [Tooltip("미션 완료/실패 후 피드백 텍스트가 표시되는 시간")]
    [SerializeField] private float feedbackDuration = 5.0f;
    [Tooltip("다음 단계로 넘어가기 전 대기 시간")]
    [SerializeField] private float nextStepDuration = 1.0f;

    [Header("Zone Objects")]
    [Tooltip("마지막 탈출 단계에서 활성화될 목표 지점")]
    [SerializeField] private GameObject escapeZone; // (실제 코드에서 활성화 로직은 보이지 않음)

    // 게임 진행 단계 정의 (열거형)
    public enum GamePhase { Caution, Tutorial, Move1, ABCPose, Move2, HoldPillar, ClimbUp, Escape, Finished }

    [Header("Debug Info")]
    [SerializeField] private GamePhase currentPhase = GamePhase.Caution; // 현재 진행 단계 (Inspector 확인용)

    // =================================================================================
    // 내부 상태 변수
    // =================================================================================
    private bool isZoneReached = false;       // 목적지 도달(트리거) 여부 플래그
    private float currentActionHoldTimer = 0f; // 액션(자세 유지) 누적 타이머
    private bool isActionCompleted = false;    // 액션(버티기) 성공 여부 플래그

    // =================================================================================
    // Unity 생명 주기 메서드
    // =================================================================================

    private void Start()
    {
        StartCoroutine(ScenarioRoutine()); // 메인 시나리오 코루틴 시작
    }

    /// <summary>
    /// 외부(Trigger 등)에서 목적지 도달 여부를 설정할 때 호출됩니다.
    /// </summary>
    public void SetZoneReached(bool reached)
    {
        isZoneReached = reached;
    }

    #region UI Helper Coroutines (UI 제어 보조 코루틴)

    /// <summary>
    /// 미션 안내 텍스트를 띄우고 일정 시간 대기하는 코루틴입니다.
    /// </summary>
    private IEnumerator ShowStepTextAndDelay(string instructionText, string missionText)
    {
        // 1. 플레이어 이동 비활성화 (UI 집중 유도)
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(false);

        // 2. UI 설정 및 표시
        if (uiManager)
        {
            uiManager.UpdateFeedBack("");
            uiManager.UpdateInstruction(instructionText);
            uiManager.UpdateMission(missionText);
            uiManager.OpenInstructionPanel(); // 안내 패널 열기
        }

        // 3. 설정된 시간만큼 대기 (또는 A 버튼 입력 등으로 패널이 닫힐 때까지)
        float timer = 0f;
        while (uiManager.GetDisplayPanel() && timer < instructionDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 4. 패널 닫기 (A 버튼으로 이미 닫았다면 스킵)
        if (uiManager && uiManager.GetDisplayPanel()) uiManager.CloseInstructionPanel();

        // 5. 이동 재활성화
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(true);
    }

    /// <summary>
    /// 결과 피드백 텍스트를 띄우고 일정 시간 대기하는 코루틴입니다.
    /// </summary>
    private IEnumerator ShowFeedbackAndDelay(string feedbackText)
    {
        // 1. 이동 비활성화
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(false);

        // 2. UI 설정 (피드백 표시)
        if (uiManager)
        {
            uiManager.UpdateInstruction("");
            uiManager.UpdateMission("");
            uiManager.UpdateFeedBack(feedbackText);
            uiManager.OpenInstructionPanel();
        }

        // 3. 대기
        float timer = 0f;
        while (uiManager.GetDisplayPanel() && timer < feedbackDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 4. 패널 닫기
        if (uiManager && uiManager.GetDisplayPanel()) uiManager.CloseInstructionPanel();

        // 5. 이동 재활성화
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(true);
    }

    /// <summary>
    /// 제한 시간이 있는 미션을 UI 타이머/게이지와 함께 실행하는 코루틴입니다.
    /// </summary>
    private IEnumerator ShowTimedMission(string missionText, System.Func<bool> missionCondition, System.Func<float> progressCalculator = null)
    {
        // IngameUIManager의 StartMissionTimer 코루틴을 시작하고 완료를 기다림
        Coroutine timerCoroutine = uiManager.StartCoroutine(uiManager.StartMissionTimer(
            missionText,
            phaseTime, // 최대 제한 시간
            missionCondition,
            progressCalculator // null이면 타이머 모드, 아니면 게이지 모드
        ));

        yield return timerCoroutine;
    }

    #endregion

    #region Logic Helper Coroutines (미션 논리 보조 코루틴)

    /// <summary>
    /// 지속적인 행동(자세 유지 등)을 감시하고 누적 시간을 계산하는 코루틴입니다.
    /// </summary>
    /// <param name="actionCondition">성공 판정 조건 함수 (예: 자세가 올바른가?)</param>
    /// <param name="requiredDuration">목표 유지 시간</param>
    private IEnumerator MonitorContinuousAction(System.Func<bool> actionCondition, float requiredDuration)
    {
        isActionCompleted = false;
        currentActionHoldTimer = 0f;

        if (requiredDuration > 0f)
        {
            while (!isActionCompleted)
            {
                if (actionCondition.Invoke()) // 조건 충족 (액션 유지 중)
                {
                    currentActionHoldTimer += Time.deltaTime;
                }
                else // 조건 불충족 (액션 실패)
                {
                    // Decay Logic: 2배 속도로 감소시켜 잠깐의 실수에는 관대하게 처리
                    currentActionHoldTimer -= Time.deltaTime * 2.0f;
                }

                // 타이머 값 범위 제한 (0 ~ 목표 시간)
                currentActionHoldTimer = Mathf.Clamp(currentActionHoldTimer, 0f, requiredDuration);

                // 목표 시간 달성 체크
                if (currentActionHoldTimer >= requiredDuration)
                {
                    isActionCompleted = true; // 완료 플래그 설정
                    break;
                }
                yield return null;
            }
        }
    }

    #endregion

    /// <summary>
    /// 전체 게임 시나리오를 순차적으로 진행하는 메인 코루틴입니다.
    /// </summary>
    private IEnumerator ScenarioRoutine()
    {
        // ---------------------------------------------------------------------------------
        // Intro: 주의사항 패널 (A 버튼 입력으로 닫기)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Caution;
        Debug.Log("Caution Start");

        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.OpenCautionPanel();
        }
        // 패널이 닫힐 때까지 대기
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel());

        Debug.Log("Caution Close");
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 0: 튜토리얼 (이동 기초)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Tutorial;
        yield return StartCoroutine(ShowStepTextAndDelay(
            "튜토리얼: 바닥의 화살표를 따라 목표 지점으로 이동하세요.", " ")
        );
        uiManager.DisplayTipsImage(0); // 팁 이미지 표시
        yield return StartCoroutine(ShowTimedMission(
            "목표지점으로 이동",
            () => isZoneReached // 미션 완료 조건: 목적지 도달 플래그
        ));
        isZoneReached = false; // 플래그 리셋

        yield return StartCoroutine(ShowFeedbackAndDelay("튜토리얼을 완수하셨습니다!"));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 1: 1차 대각선 이동 (압박 시작)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Move1;
        uiManager.UpdatePressureGauge(3); // 압박 레벨 3 설정
        uiManager.OpenPressurePanel();    // 압박 패널 열기
        yield return StartCoroutine(ShowStepTextAndDelay(
            "행사로 인해 거리에 인파가 몰리고 있습니다.\n이동 속도가 느려지면 탈출해야 합니다.",
            "사람이 많은 곳은 피해서, 가장자리로 계속 이동하세요.")
        );

        uiManager.DisplayTipsImage(0);
        yield return StartCoroutine(ShowTimedMission(
            "대각선으로 이동",
            () => isZoneReached
        ));
        isZoneReached = false;

        uiManager.UpdatePressureGauge(2); // 압박 완화
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다. 가장자리는 비교적 안전합니다."));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 2: ABC 자세 (가슴 압박 방어)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.ABCPose;
        uiManager.UpdatePressureGauge(4); // 압박 레벨 4
        yield return StartCoroutine(ShowStepTextAndDelay(
            "압박이 느껴지고 있습니다.\n다리는 어깨너비로, 팔은 가슴 앞 공간을 확보하세요.",
            "가슴 공간 확보 자세(ABC 자세)를 취하세요.")
        );

        // 1. 자세 감지 모니터링 시작
        Coroutine monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsActionValid(), // ABC 자세 판정 조건
            targetHoldTime
        ));

        // 2. 타이머 미션 진행 (게이지 모드)
        uiManager.DisplayTipsImage(1);
        yield return StartCoroutine(ShowTimedMission(
            "ABC 자세 취하기",
            () => isActionCompleted, // 자세 유지가 목표 시간 달성 시 미션 완료
            () => currentActionHoldTimer / targetHoldTime // 진행률 계산기
        ));

        StopCoroutine(monitorCoroutine);

        // 3. 결과 처리
        uiManager.UpdatePressureGauge(3);
        // DataManager.Instance.AddSuccessCount(); // 성공 횟수 기록
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다, 압박이 조금 완화되었습니다."));

        // 상태 초기화
        isActionCompleted = false;
        if (uiManager) uiManager.SetPressureIntensity(0.0f); // 비네팅 효과 해제
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
        // Phase 4: 기둥 잡기 (넘어짐 방지)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.HoldPillar;
        uiManager.UpdatePressureGauge(4);
        yield return StartCoroutine(ShowStepTextAndDelay(
            "사람들이 넘어지고 있습니다.\n중심을 잃지 않도록 가까운 기둥을 잡으세요.",
            "기둥을 Grab 버튼으로 잡고 3초 이상 유지하세요.")
        );

        // 1. 기둥 잡기 감지 시작
        monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsHoldingClimbHandle(), // 기둥 잡기 판정
            targetHoldTime
        ));

        // 2. 타이머 미션 진행 (게이지 모드)
        uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission(
            "기둥 잡기",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime
        ));

        StopCoroutine(monitorCoroutine);

        // 3. 결과 처리
        uiManager.UpdatePressureGauge(3);
        // DataManager.Instance.AddSuccessCount();
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다. 중심을 유지했습니다."));

        isActionCompleted = false;
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 5: 구조물 오르기 (숨 고르기)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.ClimbUp;
        uiManager.UpdatePressureGauge(4);
        yield return StartCoroutine(ShowStepTextAndDelay(
            "탈출로에 가까워질수록 인파가 밀집되고 있습니다.\n구조물을 이용해 잠시 숨을 확보하세요.",
            "벽을 타고 올라가 3초 이상 유지하세요.")
        );

        // 1. 매달리기 감지 시작 (3초 유지 목표)
        monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsHoldingClimbHandle(), // 매달림(Grip) 체크 (Phase 4와 동일한 조건 함수 사용)
            3.0f // 목표 시간은 3.0f로 하드코딩
        ));

        // 2. 타이머 미션 진행 (게이지 모드)
        uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission(
            "구조물 오르기",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime // targetHoldTime (3.0f)로 계산
        ));

        StopCoroutine(monitorCoroutine);

        // 3. 결과 처리
        // bool climbSuccess = isActionCompleted; // 결과 변수는 사용되지 않음

        isZoneReached = false;
        isActionCompleted = false;

        uiManager.UpdatePressureGauge(3);
        // DataManager.Instance.AddSuccessCount();
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

        uiManager.DisplayTipsImage(0);
        yield return StartCoroutine(ShowTimedMission(
            "안전구역으로 이동",
            () => isZoneReached // 미션 완료 조건: 목적지 도달
        ));
        isZoneReached = false;

        uiManager.ClosePressurePanel(); // 압박 UI 닫기

        // ---------------------------------------------------------------------------------
        // Phase 7: 게임 종료 및 결과
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Finished;

        // 게임 클리어 처리 (GameManager 호출)
        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameClear();

        // 결과 UI 표시
        if (uiManager) uiManager.ShowOuttroUI();
    }
}