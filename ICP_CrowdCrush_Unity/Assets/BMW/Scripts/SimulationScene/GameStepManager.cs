using UnityEngine;
using System.Collections;

/// <summary>
/// [V5 Update] 게임 시나리오 흐름 제어 V2
/// - 피드백 후 10초 대기 로직 적용
/// </summary>
public class GameStepManager : MonoBehaviour
{

    [Header("Linked Managers")]
    [SerializeField] private IngameUIManager uiManager;
    [SerializeField] private GestureManager gestureManager;
    [SerializeField] private Transform playerTransform;

    [Header("Settings")]
    [SerializeField] private float targetHoldTime = 3.0f;
    [SerializeField] private float targetClimbHeight = 0.5f;
    [SerializeField] private float feedbackDuration = 10.0f;
    [SerializeField] private float nextStepDuration = 2.0f;

    [Header("Zone Objects")]
    [SerializeField] private GameObject escapeZone;

    public enum GamePhase { Caution, Tutorial, Move1, ABCPose, Move2, HoldPillar, ClimbUp, Escape, Finished }
    private GamePhase currentPhase = GamePhase.Caution;
    private float currentTimer = 0f;
    private bool isZoneReached = false;

    private void Start()
    {
        StartCoroutine(ScenarioRoutine());
    }

    public void SetZoneReached(bool reached)
    {
        isZoneReached = reached;
    }

    // --- 헬퍼 함수: 피드백 보여주고 대기 후 다음으로 ---
    private IEnumerator ShowFeedbackAndDelay(string feedbackText)
    {
        // 1. 피드백 텍스트 설정 및 패널 열기
        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.UpdateFeedBack(feedbackText);
            uiManager.OpenInstructionPanel();
        }

        // 2. 10초 대기
        yield return new WaitForSeconds(feedbackDuration);

        // 3. 패널 닫기 (다음 미션을 위해)
        if (uiManager) uiManager.CloseInstructionPanel();
        uiManager.SetDisplayPanel(false);
    }

    private IEnumerator ScenarioRoutine()
    {
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

        // =================================================================================
        // Phase 0: 튜토리얼
        // =================================================================================
        currentPhase = GamePhase.Tutorial;
        Debug.Log("Phase 0 Start");
        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.UpdateInstruction("튜토리얼: 바닥의 화살표를 따라 목표 지점으로 이동하세요.");
            uiManager.UpdateMission(" ");
            uiManager.OpenInstructionPanel();
            yield return new WaitUntil(() => !uiManager.GetDisplayPanel());
        }

        yield return new WaitUntil(() => isZoneReached);
        isZoneReached = false;

        yield return StartCoroutine(ShowFeedbackAndDelay("튜토리얼을 완수하셨습니다!"));
        Debug.Log("Phase 0 Clear");
        yield return new WaitForSeconds(nextStepDuration);

        // =================================================================================
        // Phase 1: 1차 대각선 이동
        // =================================================================================
        currentPhase = GamePhase.Move1;
        Debug.Log("Phase 1 Start");
        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.UpdateInstruction("행사로 인해 거리에 인파가 몰리고 있습니다.\n이동 속도가 느려지면 탈출해야 합니다.");
            uiManager.UpdateMission("사람이 많은 곳은 피해서, 가장자리로 계속 이동하세요.");
            uiManager.OpenInstructionPanel(); // 미션 보여주기
            yield return new WaitUntil(() => !uiManager.GetDisplayPanel());
        }

        yield return new WaitUntil(() => isZoneReached);
        isZoneReached = false;

        // [수정] 피드백 처리
        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다. 가장자리는 비교적 안전합니다."));
        Debug.Log("Phase 1 Clear");
        yield return new WaitForSeconds(nextStepDuration);

        // =================================================================================
        // Phase 2: ABC 자세
        // =================================================================================
        currentPhase = GamePhase.ABCPose;
        Debug.Log("Phase 2 Start");
        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.UpdateInstruction("압박이 느껴지고 있습니다.\n다리는 어깨너비로, 팔은 가슴 앞 공간을 확보하세요.");
            uiManager.UpdateMission("가슴 공간 확보 자세(ABC 자세)를 취하세요.");
            uiManager.OpenInstructionPanel();
            uiManager.SetPressureIntensity(0.8f);
            yield return new WaitUntil(() => !uiManager.GetDisplayPanel());
        }

        currentTimer = 0f;
        while (currentTimer < targetHoldTime)
        {
            bool isActionValid = false;
            // GestureManager & Button Check
            if (gestureManager != null && gestureManager.IsActionValid()) isActionValid = true;

            if (isActionValid)
            {
                currentTimer += Time.deltaTime;
                gestureManager.TriggerHapticFeedback(currentTimer / targetHoldTime);
            }
            else
            {
                currentTimer = Mathf.Max(0, currentTimer - Time.deltaTime * 2);
            }

            if (uiManager) uiManager.UpdateActionGauge(currentTimer / targetHoldTime);
            yield return null;
        }

        if (uiManager)
        {
            uiManager.SetPressureIntensity(0.0f);
            uiManager.UpdateActionGauge(0f);
        }
        if (DataManager.Instance != null) DataManager.Instance.AddSuccessCount();

        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다, 압박이 조금 완화되었습니다."));
        Debug.Log("Phase 2 Clear");
        yield return new WaitForSeconds(nextStepDuration);

        // =================================================================================
        // Phase 3: 2차 대각선 이동
        // =================================================================================
        currentPhase = GamePhase.Move2;
        Debug.Log("Phase 3 Start");
        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.UpdateInstruction("다시 인파가 몰리고 있습니다.\n즉시 탈출해야 합니다.");
            uiManager.UpdateMission("사람이 많은 곳은 피해서, 가장자리로 계속 이동하세요.");
            uiManager.OpenInstructionPanel();
            yield return new WaitUntil(() => !uiManager.GetDisplayPanel());
        }

        yield return new WaitUntil(() => isZoneReached);
        isZoneReached = false;

        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다. 가장자리는 비교적 안전합니다."));
        Debug.Log("Phase 3 Clear");
       yield return new WaitForSeconds(nextStepDuration);

        // =================================================================================
        // Phase 4: 기둥 잡기
        // =================================================================================
        currentPhase = GamePhase.HoldPillar;
        Debug.Log("Phase 4 Start");
        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.UpdateInstruction("사람들이 넘어지고 있습니다.\n중심을 잃지 않도록 가까운 기둥을 잡으세요.");
            uiManager.UpdateMission("기둥을 Grab 버튼으로 잡고 3초 이상 유지하세요.");
            uiManager.OpenInstructionPanel();
            uiManager.SetCameraShake(true);
            yield return new WaitUntil(() => !uiManager.GetDisplayPanel());
        }

        currentTimer = 0f;
        while (currentTimer < targetHoldTime)
        {
            bool isGripping = false;
            if (ControllerInputManager.Instance != null)
            {
                isGripping = ControllerInputManager.Instance.IsLeftGripHeld ||
                             ControllerInputManager.Instance.IsRightGripHeld;
            }

            if (isGripping)
            {
                currentTimer += Time.deltaTime;
                if (gestureManager) gestureManager.TriggerHapticFeedback(currentTimer / targetHoldTime);
            }
            else
            {
                currentTimer = Mathf.Max(0, currentTimer - Time.deltaTime * 2);
            }

            if (uiManager) uiManager.UpdateActionGauge(currentTimer / targetHoldTime);
            yield return null;
        }

        if (uiManager)
        {
            uiManager.SetCameraShake(false);
            uiManager.UpdateActionGauge(0f);
        }
        if (DataManager.Instance != null) DataManager.Instance.AddSuccessCount();

        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다. 중심을 유지했습니다."));
        Debug.Log("Phase 4 Clear");
       yield return new WaitForSeconds(nextStepDuration);

        // =================================================================================
        // Phase 5: 구조물 오르기
        // =================================================================================
        currentPhase = GamePhase.ClimbUp;
        Debug.Log("Phase 5 Start");
        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.UpdateInstruction("탈출로에 가까워질수록 인파가 밀집되고 있습니다.\n구조물을 이용해 잠시 숨을 확보하세요.");
            uiManager.UpdateMission("벽을 타고 올라가 3초 이상 유지하세요.");
            uiManager.OpenInstructionPanel();
            yield return new WaitUntil(() => !uiManager.GetDisplayPanel());
        }

        float initialY = playerTransform.position.y;
        currentTimer = 0f;

        while (currentTimer < 2.0f)
        {
            float currentHeight = playerTransform.position.y - initialY;
            bool isHighEnough = currentHeight >= targetClimbHeight;
            bool isGripping = false;
            if (ControllerInputManager.Instance != null)
            {
                isGripping = ControllerInputManager.Instance.IsLeftGripHeld ||
                             ControllerInputManager.Instance.IsRightGripHeld;
            }

            if (isHighEnough && isGripping)
            {
                currentTimer += Time.deltaTime;
            }
            else
            {
                currentTimer = 0f;
            }
            yield return null;
        }

        if (DataManager.Instance != null) DataManager.Instance.AddSuccessCount();

        yield return StartCoroutine(ShowFeedbackAndDelay("잘하셨습니다, 지금은 잠시 안전합니다. 숨을 고르세요."));
        Debug.Log("Phase 5 Clear");
       yield return new WaitForSeconds(nextStepDuration);

        // =================================================================================
        // Phase 6: 탈출
        // =================================================================================
        currentPhase = GamePhase.Escape;
        Debug.Log("Phase 6 Start");
        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.UpdateInstruction("인파가 밀집 공간에서 벗어났습니다.\n이제 안전한 곳으로 이동하세요.");
            uiManager.UpdateMission("경찰/구조대가 있는 안전 구역으로 이동하세요.");
            uiManager.OpenInstructionPanel();
            yield return new WaitUntil(() => !uiManager.GetDisplayPanel());
        }

        if (escapeZone) escapeZone.SetActive(true);

        yield return new WaitUntil(() => isZoneReached);
        isZoneReached = false;

        // 탈출 성공 시에는 10초 대기 없이 바로 결과창으로
        Debug.Log("Phase 6 Clear");

        // =================================================================================
        // Phase 7: 종료
        // =================================================================================
        currentPhase = GamePhase.Finished;

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameClear();

        if (uiManager) uiManager.ShowOuttroUI();
    }
}