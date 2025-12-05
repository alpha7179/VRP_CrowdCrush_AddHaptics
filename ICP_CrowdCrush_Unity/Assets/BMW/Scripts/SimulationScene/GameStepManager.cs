using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// ������ ��ü �ó����� �帧(Ʃ�丮�� -> �̵� -> �׼� -> Ż��)�� ���������� �����ϴ� ���� �Ŵ����Դϴ�.
/// <para>
/// 1. �� ���� ������(Phase)���� �̼��� �ο��ϰ� ����/���и� �����մϴ�.<br/>
/// 2. PlayerManager�� ���� �̵�(Locomotion) ������ �̼� �߿��� �ο��մϴ�.<br/>
/// 3. IngameUIManager�� �����Ͽ� �ȳ� �ؽ�Ʈ, Ÿ�̸�, �ǵ���� ǥ���մϴ�.
/// </para>
/// </summary>
public class GameStepManager : MonoBehaviour
{
    #region Inspector Settings (References)

    [Header("Player References")]
    [Tooltip("�÷��̾��� Transform (��ġ ���¿�)")]
    [SerializeField] private Transform PlayerTransform;

    [Header("Linked Managers")]
    [Tooltip("�ΰ��� UI ��� ����ϴ� �Ŵ���")]
    [SerializeField] private IngameUIManager uiManager;

    [Tooltip("�÷��̾��� ����ó �� �Է� ������ ����ϴ� �Ŵ���")]
    [SerializeField] private GestureManager gestureManager;

    [Header("Zone Objects")]
    [Tooltip("�� �ܰ迡�� Ȱ��ȭ�� ��ǥ ���� Ʈ���� ������Ʈ�� (0:Tutorial, 1:Move1, 2:Move2, 3:Escape)")]
    [SerializeField] private GameObject[] TargerZone;

    #endregion

    #region Inspector Settings (Game Logic)

    [Header("Action Settings")]
    [Tooltip("�׼�(�ڼ� ����, ��� ��)�� �����ϱ� ���� �����ؾ� �ϴ� ��ǥ �ð� (��)")]
    [SerializeField] private float targetHoldTime = 3.0f;

    [Header("Timing Settings")]
    [Tooltip("�� ������(�̼�)�� ���� �ð�")]
    [SerializeField] private float phaseTime = 60.0f;

    [Tooltip("�̼� ���� �� �ȳ� �ؽ�Ʈ�� ǥ�õǴ� �ð�")]
    [SerializeField] private float instructionDuration = 5.0f;

    [Tooltip("�̼� �Ϸ�/���� �� �ǵ�� �ؽ�Ʈ�� ǥ�õǴ� �ð�")]
    [SerializeField] private float feedbackDuration = 5.0f;

    [Tooltip("���� �ܰ�� �Ѿ�� �� ��� �ð�")]
    [SerializeField] private float nextStepDuration = 1.0f;

    #endregion

    #region Internal State & Debug

    private enum GamePhase
    {
        Caution, Tutorial, Move1, ABCPose, Move2, HoldPillar, ClimbUp, Escape, Finished
    }

    [Header("Debug Info")]
    [SerializeField] private GamePhase currentPhase;

    // ���� ���� ����
    private bool isZoneReached = false;        // ��ǥ ���� ���� ����
    private bool isActionCompleted = false;    // �׼�(�ڼ�/���) �Ϸ� ����
    private float currentActionHoldTimer = 0f; // ���� �׼� ���� �ð�
    private int targetIndex;                   // ���� ��ǥ ���� �ε���
    private Vector3 startPosition;             // ��ġ ������ ���� ����� ��ġ

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // ���� ���� �� �ó����� �ڷ�ƾ ����
        StartCoroutine(ScenarioRoutine());
    }

    #endregion

    #region Public API

    /// <summary>
    /// ZoneTrigger���� ȣ���Ͽ� ��ǥ ������ ���������� �˸��ϴ�.
    /// </summary>
    public void SetZoneReached(bool reached)
    {
        isZoneReached = reached;
    }

    /// <summary>
    /// ���� �÷��̾��� ��ġ�� �����մϴ�. (���� ���� ���� �� üũ����Ʈ)
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
    /// �÷��̾ ����� ��ġ�� �ǵ����� ��� �ǵ���� ǥ���մϴ�.
    /// </summary>
    public void ReturnToSavedPosition()
    {
        // �ߺ� ���� ������ ���� ���� �ڷ�ƾ ���� �� �����
        StopCoroutine(ReturnToSavedPositionRoutine());
        StartCoroutine(ReturnToSavedPositionRoutine());
    }

    #endregion

    #region UI & Logic Helper Coroutines

    /// <summary>
    /// �̼� �ȳ� �ؽ�Ʈ�� ȭ�鿡 ���� ���� �ð� ����մϴ�.
    /// </summary>
    private IEnumerator ShowStepTextAndDelay(int instructionText)
    {
        // 1. UI ���� �� ǥ��
        if (uiManager)
        {
            uiManager.CloseFeedBack();
            uiManager.UpdateInstruction(instructionText);
            uiManager.OpenInstructionPanel();
        }

        // 2. ������ �ð���ŭ ���
        float timer = 0f;
        while (uiManager.GetDisplayPanel() && timer < instructionDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 3. �г� �ݱ�
        if (uiManager && uiManager.GetDisplayPanel())
            uiManager.CloseInstructionPanel();
    }

    /// <summary>
    /// ��� �ǵ�� �ؽ�Ʈ�� ���� ���� �ð� ����մϴ�.
    /// </summary>
    private IEnumerator ShowFeedbackAndDelay(int feedbackText, bool isNegative = false)
    {
        // 1. UI ����
        if (uiManager)
        {
            uiManager.CloseInstruction();
            if(!isNegative) uiManager.UpdateFeedBack(feedbackText);
            else uiManager.UpdateNegativeFeedback(feedbackText);
            uiManager.OpenInstructionPanel();
        }

        // 2. ���
        float timer = 0f;
        while (uiManager.GetDisplayPanel() && timer < feedbackDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // 3. �г� �ݱ�
        if (uiManager && uiManager.GetDisplayPanel())
            uiManager.CloseInstructionPanel();
    }

    /// <summary>
    /// ���� �ð��� �ִ� �̼��� �����մϴ�. �̼� �߿��� �̵�(Locomotion)�� ���˴ϴ�.
    /// </summary>
    private IEnumerator ShowTimedMission(string missionText, System.Func<bool> missionCondition, System.Func<float> progressCalculator = null, bool isDisplayPanel = false)
    {
        // [�ٽ� ����] �̼� ���� -> �̵� ���
        /*
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetLocomotion(true);
            PlayerManager.Instance.SetInteraction(true);
        }
        */

        // UI �Ŵ����� Ÿ�̸� �ڷ�ƾ ���� (���� �޼� �ñ��� ���)
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
            // UI �Ŵ����� ���� ��츦 ����� ������ġ (���Ǹ� ��ٸ�)
            yield return new WaitUntil(missionCondition);
        }

        // [�ٽ� ����] �̼� ���� -> �̵� ���
        /*
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetLocomotion(false);
            PlayerManager.Instance.SetInteraction(false);
        }
        */
    }

    /// <summary>
    /// Ư�� �׼�(����)�� ���� �ð� ���� ���ӵǴ��� �����մϴ�.
    /// </summary>
    private IEnumerator MonitorContinuousAction(System.Func<bool> actionCondition, float requiredDuration)
    {
        isActionCompleted = false;
        currentActionHoldTimer = 0f;

        if (requiredDuration > 0f)
        {
            while (!isActionCompleted)
            {
                // ������ �����ϸ� Ÿ�̸� ����, �ƴϸ� ����(������)
                if (actionCondition.Invoke())
                {
                    currentActionHoldTimer += Time.deltaTime;
                }
                else
                {
                    currentActionHoldTimer -= Time.deltaTime * 2.0f;
                }

                currentActionHoldTimer = Mathf.Clamp(currentActionHoldTimer, 0f, requiredDuration);

                // ��ǥ �ð� ���� �� �Ϸ� ó��
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
    /// �÷��̾� ��ġ ���� �� ��� ǥ�ø� ó���ϴ� �ڷ�ƾ
    /// </summary>
    private IEnumerator ReturnToSavedPositionRoutine()
    {
        // 1. �̵� ���
        
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(false);
        

        // 2. ��� �ǵ�� ǥ��
        yield return StartCoroutine(ShowFeedbackAndDelay(0,true));

        // 3. ��ġ �̵�
        if (PlayerTransform != null && startPosition != Vector3.zero)
        {
            PlayerTransform.position = startPosition;
            Debug.Log($"[GameStepManager] Player returned to saved position: {startPosition}");
        }
        else
        {
            Debug.LogWarning("[GameStepManager] Cannot return to saved position (Invalid data).");
        }

        // 4. �̵� ��Ȱ��ȭ (�̼� ���̹Ƿ� �ٽ� ����)
        
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.SetLocomotion(true);
        
    }

    #endregion

    #region Main Scenario Coroutine

    /// <summary>
    /// ������ ��ü �ó������� �ܰ躰�� �����ϴ� ���� �ڷ�ƾ�Դϴ�.
    /// </summary>
    private IEnumerator ScenarioRoutine()
    {
        // 0. �ʱ�ȭ
        DataManager.Instance.InitializeSessionData();

        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetLocomotion(true);
            PlayerManager.Instance.SetInteraction(true);
        }

        // ---------------------------------------------------------------------------------
        // Intro: ���ǻ��� �г�
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Caution;
        Debug.Log("[Scenario] Caution Phase Start");

        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.OpenCautionPanel();
        }

        // �г��� ���� ������ ���
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel());

        yield return new WaitForSeconds(nextStepDuration);

        if (uiManager)
        {
            uiManager.SetDisplayPanel(true);
            uiManager.OpenSituationPanel();
        }

        // �г��� ���� ������ ���
        yield return new WaitUntil(() => !uiManager.GetDisplayPanel());

        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 0: Ʃ�丮�� (�⺻ �̵�)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Tutorial;

        yield return StartCoroutine(ShowStepTextAndDelay(0));

        targetIndex = 0;
        SetZoneActive(targetIndex, true);
        if (uiManager) uiManager.DisplayTipsImage(2);

        // �̼� ���� (�̵� ����)
        yield return StartCoroutine(ShowTimedMission(
            "��ǥ�������� �̵�",
            () => isZoneReached
        ));

        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        yield return StartCoroutine(ShowFeedbackAndDelay(0));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 1: 1�� �̵� (�밢�� / �����ڸ�)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Move1;

        targetIndex = 1;
        SetZoneActive(targetIndex, true);
        yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager)
        {
            uiManager.UpdatePressureGauge(3);
            uiManager.OpenPressurePanel();
        }
        SavePlayerPosition();

        yield return StartCoroutine(ShowStepTextAndDelay(1));

        targetIndex = 2;
        SetZoneActive(targetIndex, true);
        if (uiManager) uiManager.DisplayTipsImage(2);

        yield return StartCoroutine(ShowTimedMission(
            "�밢������ �̵�",
            () => isZoneReached
        ));

        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(2);
        yield return StartCoroutine(ShowFeedbackAndDelay(1));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 2: ABC �ڼ� (���� �й� ����)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.ABCPose;

        targetIndex = 3;
        SetZoneActive(targetIndex, true);
        yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(4);

        yield return StartCoroutine(ShowStepTextAndDelay(2));

        // �ڼ� ���� ����͸� ����
        Coroutine monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsActionValid(),
            targetHoldTime
        ));

        if (uiManager) uiManager.DisplayTipsImage(0);

        yield return StartCoroutine(ShowTimedMission(
            "ABC �ڼ� ���ϱ�",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime,
            true
        ));

        StopCoroutine(monitorCoroutine);

        if (uiManager)
        {
            uiManager.UpdatePressureGauge(3);
            uiManager.SetPressureIntensity(0.0f);
        }

        yield return StartCoroutine(ShowFeedbackAndDelay(2));

        isActionCompleted = false;
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 3: 2�� �̵� (Ż�� ����)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Move2;

        if (uiManager) uiManager.UpdatePressureGauge(4);
        SavePlayerPosition();

        yield return StartCoroutine(ShowStepTextAndDelay(3));

        targetIndex = 4;
        SetZoneActive(targetIndex, true);
        if (uiManager) uiManager.DisplayTipsImage(2);

        yield return StartCoroutine(ShowTimedMission(
            "�밢������ �̵�",
            () => isZoneReached
        ));

        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(3);
        yield return StartCoroutine(ShowFeedbackAndDelay(3));
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 4: ��� ��� (�Ѿ��� ����)
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.HoldPillar;

        targetIndex = 5;
        SetZoneActive(targetIndex, true);
        yield return new WaitUntil(() => isZoneReached);
        SetZoneActive(targetIndex, false);
        isZoneReached = false;

        if (uiManager) uiManager.UpdatePressureGauge(4);

        yield return StartCoroutine(ShowStepTextAndDelay(4));

        monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsHoldingClimbHandle(),
            targetHoldTime
        ));

        if (uiManager) uiManager.DisplayTipsImage(1);

        targetIndex = 6;
        SetZoneActive(targetIndex, true);

        yield return StartCoroutine(ShowTimedMission(
            "��� ���",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime,
            true
        ));

        StopCoroutine(monitorCoroutine);

        SetZoneActive(targetIndex, false);

        if (uiManager) uiManager.UpdatePressureGauge(3);
        yield return StartCoroutine(ShowFeedbackAndDelay(4));

        isActionCompleted = false;
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 5: ����� (�� Ȯ��)
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

        // ������ ������ HoldPillar�� �����ϰ� ClimbHandle�� ��� �ִ� ������ �Ǵ�
        monitorCoroutine = StartCoroutine(MonitorContinuousAction(
            () => gestureManager.IsHoldingClimbHandle(),
            targetHoldTime
        ));

        if (uiManager) uiManager.DisplayTipsImage(1);

        yield return StartCoroutine(ShowTimedMission(
            "�� ���",
            () => isActionCompleted,
            () => currentActionHoldTimer / targetHoldTime,
            true
        ));

        StopCoroutine(monitorCoroutine);

        SetZoneActive(targetIndex, false);

        if (uiManager) uiManager.UpdatePressureGauge(3);
        yield return StartCoroutine(ShowFeedbackAndDelay(5));

        isZoneReached = false;
        isActionCompleted = false;
        yield return new WaitForSeconds(nextStepDuration);

        // ---------------------------------------------------------------------------------
        // Phase 6: ���� Ż��
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Escape;
        if (uiManager) uiManager.UpdatePressureGauge(2);

        yield return StartCoroutine(ShowStepTextAndDelay(6));

        targetIndex = 9;
        SetZoneActive(targetIndex, true);
        if (uiManager) uiManager.DisplayTipsImage(2);

        yield return StartCoroutine(ShowTimedMission(
            "������������ �̵�",
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
        // Phase 7: ���� ����
        // ---------------------------------------------------------------------------------
        currentPhase = GamePhase.Finished;
        Debug.Log("[Scenario] Game Finished");

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameClear();

        if (uiManager) uiManager.ShowOuttroUI();
    }

    /// <summary>
    /// ��ǥ ���� ������Ʈ�� Ȱ��ȭ/��Ȱ��ȭ�ϴ� ���� �޼���
    /// </summary>
    private void SetZoneActive(int index, bool isActive)
    {
        if (TargerZone != null && TargerZone.Length > index && TargerZone[index] != null)
        {
            TargerZone[index].SetActive(isActive);
        }
    }

    #endregion
}