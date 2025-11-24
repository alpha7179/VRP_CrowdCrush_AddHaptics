using UnityEngine;
using System.Collections;

/// <summary>
/// [V5] 6단계 생존 시나리오의 흐름(Flow)을 제어하는 핵심 FSM 매니저.
/// 각 단계별 진입, 수행, 성공 판정 및 연출 제어를 담당합니다.
/// </summary>
public class GameStepManager : MonoBehaviour
{
    [Header("Linked Managers")]
    [SerializeField] private GameUIManager uiManager;
    [SerializeField] private GestureManager gestureManager;
    // [SerializeField] private StageManager stageManager; // 환경 제어용 (필요 시 연결)

    [Header("Settings")]
    [SerializeField] private float targetHoldTime = 3.0f; // 자세 유지 목표 시간

    // 시나리오 단계 정의
    public enum GamePhase
    {
        Tutorial,       // 0. 튜토리얼
        Move1,          // 1. 1차 이동
        ABCPose,        // 2. ABC 자세 (핵심)
        Move2,          // 3. 2차 이동
        HoldPillar,     // 4. 기둥 잡기
        ClimbUp,        // 5. 구조물 오르기
        Escape,         // 6. 탈출
        Finished        // 7. 종료
    }

    private GamePhase currentPhase = GamePhase.Tutorial;
    private float currentTimer = 0f;

    // 외부에서 트리거(도착) 신호를 받기 위한 플래그
    private bool isZoneReached = false;

    private void Start()
    {
        // 게임 시작 시 시나리오 코루틴 가동
        StartCoroutine(ScenarioRoutine());
    }

    /// <summary>
    /// 존(Zone) 트리거 스크립트에서 호출 (예: GoalZone에 닿았을 때)
    /// </summary>
    public void SetZoneReached(bool reached)
    {
        isZoneReached = reached;
    }

    private IEnumerator ScenarioRoutine()
    {
        // =================================================================================
        // Phase 0: 튜토리얼
        // =================================================================================
        currentPhase = GamePhase.Tutorial;
        uiManager.UpdateInstruction("튜토리얼: 바닥의 화살표를 따라 목표 지점으로 이동하세요.");
        isZoneReached = false;

        // 이동 완료 대기
        yield return new WaitUntil(() => isZoneReached);
        Debug.Log("Phase 0 Clear: Tutorial");
        yield return new WaitForSeconds(1.0f); // 잠시 대기


        // =================================================================================
        // Phase 1: 1차 대각선 이동
        // =================================================================================
        currentPhase = GamePhase.Move1;
        uiManager.UpdateInstruction("인파를 피해 가장자리로 대각선 이동하세요!");
        // TODO: StageManager.SetCrowdDensity(Low);
        isZoneReached = false;

        yield return new WaitUntil(() => isZoneReached);
        Debug.Log("Phase 1 Clear: Diagonal Move 1");
        yield return new WaitForSeconds(1.0f);


        // =================================================================================
        //  Phase 2: ABC 자세 (입력 이중화 적용)
        // =================================================================================
        currentPhase = GamePhase.ABCPose;
        uiManager.UpdateInstruction("압박이 심합니다! 가슴 공간을 확보(ABC)하거나\n양쪽 트리거를 꾹 누르세요! (3초)");
        uiManager.SetPressureIntensity(0.8f); // 붉은 비네팅 (위기 연출)

        currentTimer = 0f;
        while (currentTimer < targetHoldTime)
        {
            // GestureManager를 통해 제스처 또는 버튼 입력 확인 (Dual Validation)
            if (gestureManager.IsActionValid())
            {
                currentTimer += Time.deltaTime;
                // 햅틱 피드백 (진행될수록 강하게)
                gestureManager.TriggerHapticFeedback(currentTimer / targetHoldTime);
            }
            else
            {
                // 입력을 멈추면 게이지 서서히 감소
                currentTimer = Mathf.Max(0, currentTimer - Time.deltaTime * 2);
            }

            uiManager.UpdateActionGauge(currentTimer / targetHoldTime); // UI 게이지 갱신
            yield return null;
        }

        // 성공 처리
        uiManager.SetPressureIntensity(0.0f); // 비네팅 해제
        uiManager.UpdateActionGauge(0f);      // 게이지 초기화
        if (DataManager.Instance != null) DataManager.Instance.AddSuccessCount(); // 데이터 저장
        Debug.Log("Phase 2 Clear: ABC Pose");
        yield return new WaitForSeconds(1.0f);


        // =================================================================================
        //  Phase 3: 2차 대각선 이동
        // =================================================================================
        currentPhase = GamePhase.Move2;
        uiManager.UpdateInstruction("다시 인파가 몰려옵니다. 벽 쪽 틈새로 이동하세요.");
        // TODO: StageManager.SetCrowdDensity(High);
        isZoneReached = false;

        yield return new WaitUntil(() => isZoneReached);
        Debug.Log("Phase 3 Clear: Diagonal Move 2");
        yield return new WaitForSeconds(1.0f);


        // =================================================================================
        //  Phase 4: 기둥 잡기
        // =================================================================================
        currentPhase = GamePhase.HoldPillar;
        uiManager.UpdateInstruction("넘어지지 않도록 기둥을 잡고(Grip) 버티세요!");
        uiManager.SetCameraShake(true); // 화면 흔들림 연출

        currentTimer = 0f;
        while (currentTimer < targetHoldTime)
        {
            // Grip 버튼 입력 확인 (ControllerInputManager2 싱글톤 활용)
            // 추가로 손이 기둥 Collider 안에 있는지 체크하는 로직이 있으면 더 좋음
            bool isGripping = false;
            if (ControllerInputManager.Instance != null)
            {
                isGripping = ControllerInputManager.Instance.IsLeftGripHeld ||
                             ControllerInputManager.Instance.IsRightGripHeld;
            }

            if (isGripping)
            {
                currentTimer += Time.deltaTime;
                gestureManager.TriggerHapticFeedback(currentTimer / targetHoldTime);
            }
            else
            {
                currentTimer = Mathf.Max(0, currentTimer - Time.deltaTime * 2);
            }

            uiManager.UpdateActionGauge(currentTimer / targetHoldTime);
            yield return null;
        }

        uiManager.SetCameraShake(false); // 흔들림 멈춤
        uiManager.UpdateActionGauge(0f);
        if (DataManager.Instance != null) DataManager.Instance.AddSuccessCount();
        Debug.Log("Phase 4 Clear: Hold Pillar");
        yield return new WaitForSeconds(1.0f);


        // =================================================================================
        // Phase 5: 구조물 오르기
        // =================================================================================
        currentPhase = GamePhase.ClimbUp;
        uiManager.UpdateInstruction("바닥은 위험합니다! 구조물을 타고 위로 올라가세요.");

        // TODO: 실제로는 플레이어의 Y축 높이(transform.position.y)를 체크해야 함
        yield return new WaitForSeconds(3.0f); // 임시 대기 (자동 성공)

        if (DataManager.Instance != null) DataManager.Instance.AddSuccessCount();
        Debug.Log("Phase 5 Clear: Climb Up");


        // =================================================================================
        // Phase 6: 탈출
        // =================================================================================
        currentPhase = GamePhase.Escape;
        uiManager.UpdateInstruction("경찰/구조대가 있는 안전 구역으로 이동하세요.");
        isZoneReached = false;

        yield return new WaitUntil(() => isZoneReached);
        Debug.Log("Phase 6 Clear: Escape");


        // =================================================================================
        //  Phase 7: 종료
        // =================================================================================
        currentPhase = GamePhase.Finished;

        // GameManager에게 클리어 알림
        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameClear();

        // 결과창 호출
        uiManager.ShowOuttroUI();
    }
}