using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 게임의 전체 시나리오 흐름(튜토리얼 -> 이동 -> 액션 -> 탈출)을 제어하는 메인 매니저
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
    [Tooltip("마지막 탈출 단계에서 활성화될 목표 지점")]
    [SerializeField] private GameObject escapeZone;

    // 게임 진행 단계 정의
    public enum GamePhase { Caution, Tutorial, Move1, ABCPose, Move2, HoldPillar, ClimbUp, Escape, Finished }

    [Header("Debug Info")]
    [SerializeField] private GamePhase currentPhase = GamePhase.Caution; // 현재 진행 단계 (Inspector 확인용)

    // 내부 상태 변수
    private bool isZoneReached = false;       // 목적지 도달 여부 플래그
    private float currentActionHoldTimer = 0f; // 액션 유지 시간 누적용 타이머
    private bool isActionCompleted = false;    // 액션(버티기) 성공 여부 플래그

    private void Start()
    {
        StartCoroutine(ScenarioRoutine());
    }

    /// <summary>
    /// 외부(Trigger 등)에서 목적지 도달 여부를 설정할 때 호출
    /// </summary>
    public void SetZoneReached(bool reached)
    {
        isZoneReached = reached;
    }

    #region UI Helper Coroutines

    /// <summary>
    /// 미션 안내 텍스트를 띄우고 일정 시간 대기하는 코루틴
    /// </summary>
    private IEnumerator ShowStepTextAndDelay(string instructionText, string missionText)
    {
        // 1. 이동 비활성화 (플레이어가 UI에 집중하도록 유도)
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(false);

        // 2. UI 설정 및 표시
        if (uiManager)
        {
            uiManager.UpdateFeedBack(""); // 피드백 초기화
            uiManager.UpdateInstruction(instructionText);
            uiManager.UpdateMission(missionText);
            uiManager.OpenInstructionPanel();
        }

        // 3. 설정된 시간만큼 대기 (또는 패널이 닫길 때까지)
        float timer = 0f;
        while (uiManager.GetDisplayPanel() && timer < instructionDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 4. 패널 닫기
        if (uiManager && uiManager.GetDisplayPanel()) uiManager.CloseInstructionPanel();

        // 5. 이동 재활성화 (게임 재개)
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(true);
    }

    /// <summary>
    /// 결과 피드백 텍스트를 띄우고 일정 시간 대기하는 코루틴
    /// </summary>
    private IEnumerator ShowFeedbackAndDelay(string feedbackText)
    {
        // 1. 이동 비활성화
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(false);

        // 2. UI 설정 (미션 텍스트 지우고 피드백 표시)
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
    /// 제한 시간이 있는 미션을 UI 타이머와 함께 실행하는 코루틴
    /// </summary>
    private IEnumerator ShowTimedMission(string missionText, System.Func<bool> missionCondition, System.Func<float> progressCalculator = null)
    {
        // UI 매니저 호출 시 전달받은 progressCalculator를 그대로 넘김
        Coroutine timerCoroutine = uiManager.StartCoroutine(uiManager.StartMissionTimer(
            missionText,
            phaseTime,
            missionCondition,
            progressCalculator
        ));

        yield return timerCoroutine;
    }

    #endregion

    #region Logic Helper Coroutines

    /// <summary>
    /// 지속적인 행동(버티기)을 감시하고 게이지를 업데이트하는 코루틴
    /// </summary>
    /// <param name="actionCondition">성공 판정 조건 함수 (델리게이트)</param>
    /// <param name="requiredDuration">목표 유지 시간</param>
    private IEnumerator MonitorContinuousAction(System.Func<bool> actionCondition, float requiredDuration)
    {
        isActionCompleted = false;
        currentActionHoldTimer = 0f;

        if (requiredDuration > 0f)
        {
            while (!isActionCompleted)
            {
                if (actionCondition.Invoke()) // 조건 충족 (자세 유지 중)
                {
                    currentActionHoldTimer += Time.deltaTime;
                }
                else // 조건 불충족 (손 떨림/인식 실패 보정)
                {
                    // 즉시 0이 아니라, 서서히 감소 (Decay Logic)
                    // 2배 속도로 감소시켜 잠깐의 실수에는 관대하게 처리
                    currentActionHoldTimer -= Time.deltaTime * 2.0f;
                }

                // 타이머 값 범위 제한 (0 ~ 목표 시간)
                currentActionHoldTimer = Mathf.Clamp(currentActionHoldTimer, 0f, requiredDuration);

                // 목표 시간 달성 체크
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
    /// 전체 게임 시나리오를 순차적으로 진행하는 메인 코루틴
    /// </summary>
    private IEnumerator ScenarioRoutine()
    {
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
        // 패널이 닫힐 때까지 대기 (플레이어의 버튼 입력 등)
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel());

        Debug.Log("Caution Close");
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 0: 튜토리얼 (이동 기초)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Tutorial;
        Debug.Log("Phase 0 Start");

        yield return StartCoroutine(ShowStepTextAndDelay(
            "튜토리얼: 바닥의 화살표를 따라 목표 지점으로 이동하세요.",
            " ")
        );
        uiManager.DisplayTipsImage(0);
        yield return StartCoroutine(ShowTimedMission(
            "목표지점으로 이동",
            () => isZoneReached // 미션 완료 조건: 목적지 도달
        ));
        isZoneReached = false; // 플래그 리셋

        yield return StartCoroutine(ShowFeedbackAndDelay("튜토리얼을 완수하셨습니다!"));
        Debug.Log("Phase 0 Clear");
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 1: 1차 대각선 이동
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Move1;
        Debug.Log("Phase 1 Start");

        uiManager.UpdatePressureGauge(3);
        uiManager.OpenPressurePanel();
        yield return StartCoroutine(ShowStepTextAndDelay(
            "행사로 인해 거리에 인파가 몰리고 있습니다.\n이동 속도가 느려지면 탈출해야 합니다.",
            "사람이 많은 곳은 피해서, 가장자리로 계속 이동하세요.")
        );

        uiManager.DisplayTipsImage(0);
        yield return StartCoroutine(ShowTimedMission(
            "대각선으로 이동",
            () => isZoneReached // 미션 완료 조건: 목적지 도달
        ));
        isZoneReached = false;

        uiManager.UpdatePressureGauge(2);
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다. 가장자리는 비교적 안전합니다."));
        Debug.Log("Phase 1 Clear");
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 2: ABC 자세 (가슴 압박 방어)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.ABCPose;
        Debug.Log("Phase 2 Start");

        uiManager.UpdatePressureGauge(4);
        yield return StartCoroutine(ShowStepTextAndDelay(
            "압박이 느껴지고 있습니다.\n다리는 어깨너비로, 팔은 가슴 앞 공간을 확보하세요.",
            "가슴 공간 확보 자세(ABC 자세)를 취하세요.")
        );

        // 1. 자세 감지 모니터링 시작
        Coroutine monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsActionValid(), // ABC 자세 판정
            targetHoldTime
        ));

        // 2. 타이머 미션 진행
        uiManager.DisplayTipsImage(1);
        yield return StartCoroutine(ShowTimedMission(
            "ABC 자세 취하기",
            () => isActionCompleted, // 자세 유지 완료 시 조기 종료
            () => currentActionHoldTimer / targetHoldTime
        ));

        // 3. 모니터링 종료
        StopCoroutine(monitorCoroutine);

        // 4. 결과 처리
        uiManager.UpdatePressureGauge(3);
        if (DataManager.Instance != null) DataManager.Instance.AddSuccessCount();
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다, 압박이 조금 완화되었습니다."));

        // 상태 초기화
        isActionCompleted = false;
        if (uiManager) uiManager.SetPressureIntensity(0.0f); // 비네팅 효과 해제 등

        Debug.Log("Phase 2 Clear");
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 3: 2차 대각선 이동
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Move2;
        Debug.Log("Phase 3 Start");

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
        Debug.Log("Phase 3 Clear");
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 4: 기둥 잡기 (넘어짐 방지)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.HoldPillar;
        Debug.Log("Phase 4 Start");

        uiManager.UpdatePressureGauge(4);
        yield return StartCoroutine(ShowStepTextAndDelay(
            "사람들이 넘어지고 있습니다.\n중심을 잃지 않도록 가까운 기둥을 잡으세요.",
            "기둥을 Grab 버튼으로 잡고 3초 이상 유지하세요.")
        );

        // 1. 기둥 잡기 감지 시작
        monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsHoldingClimbHandle(),
            targetHoldTime
        ));

        // 2. 타이머 미션 진행
        uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission(
            "기둥 잡기",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime
        ));

        StopCoroutine(monitorCoroutine);

        // 3. 결과 처리
        uiManager.UpdatePressureGauge(3);
        if (DataManager.Instance != null) DataManager.Instance.AddSuccessCount();
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다. 중심을 유지했습니다."));

        // 상태 초기화
        isActionCompleted = false;

        Debug.Log("Phase 4 Clear");
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 5: 구조물 오르기 (숨 고르기)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.ClimbUp;
        Debug.Log("Phase 5 Start");

        uiManager.UpdatePressureGauge(4);
        yield return StartCoroutine(ShowStepTextAndDelay(
            "탈출로에 가까워질수록 인파가 밀집되고 있습니다.\n구조물을 이용해 잠시 숨을 확보하세요.",
            "벽을 타고 올라가 3초 이상 유지하세요.")
        );

        // 1. 매달리기 감지 시작 (3초 유지 목표)
        monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsHoldingClimbHandle(), // 매달림(Grip) 체크
            3.0f
        ));

        // 2. 타이머 미션 진행
        uiManager.DisplayTipsImage(2);
        yield return StartCoroutine(ShowTimedMission(
            "구조물 오르기",
            () => isActionCompleted, // 3초 유지가 완료되었는지 확인
            () => currentActionHoldTimer / targetHoldTime
        ));

        StopCoroutine(monitorCoroutine);

        // 3. 결과 처리
        bool climbSuccess = isActionCompleted;

        // 플래그 정리
        isZoneReached = false;
        isActionCompleted = false;

        uiManager.UpdatePressureGauge(3);
        if (DataManager.Instance != null) DataManager.Instance.AddSuccessCount();
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다, 지금은 잠시 안전합니다. 숨을 고르세요."));

        Debug.Log("Phase 5 Clear");
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 6: 최종 탈출
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Escape;
        Debug.Log("Phase 6 Start");

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
        isZoneReached = false; // 플래그 리셋

        uiManager.ClosePressurePanel();
        Debug.Log("Phase 6 Clear");

        // ---------------------------------------------------------------------------------
        // Phase 7: 게임 종료 및 결과
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Finished;

        // 게임 클리어 처리 (점수 저장 등)
        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameClear();

        // 결과 UI 표시
        if (uiManager) uiManager.ShowOuttroUI();
    }
}