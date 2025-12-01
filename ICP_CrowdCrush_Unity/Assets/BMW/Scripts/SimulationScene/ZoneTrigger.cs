using UnityEngine;

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
    [Tooltip("체크 시: 다음 단계로 넘어가기 위한 목표 지점으로 동작합니다.")]
    [SerializeField] private bool isGoal = true;

    [Tooltip("체크 시: 진입하면 안 되는 위험 구역으로 동작합니다. (실수 카운트 증가, 위치 리셋)")]
    [SerializeField] private bool isDanger = false;

    [Header("Target Settings")]
    [Tooltip("감지할 플레이어의 태그입니다. (XR Origin 또는 Main Camera의 태그와 일치해야 함)")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [Tooltip("디버그 로그 출력 여부")]
    [SerializeField] private bool isDebug = true;

    #endregion

    #region Unity Lifecycle

    private void OnTriggerEnter(Collider other)
    {
        // 1. 플레이어인지 확인
        // XR Origin 구조상 Collider가 자식 객체(Hands, Head 등)에 있을 수 있으므로 Root의 태그까지 확인합니다.
        if (other.CompareTag(playerTag) || (other.transform.root != null && other.transform.root.CompareTag(playerTag)))
        {
            if (isDebug) Debug.Log($"[ZoneTrigger] Player entered trigger: {gameObject.name}");

            HandlePlayerEnter();
        }
    }

    #endregion

    #region Internal Logic

    /// <summary>
    /// 플레이어가 트리거에 진입했을 때의 로직을 처리합니다.
    /// </summary>
    private void HandlePlayerEnter()
    {
        // 씬에 있는 GameStepManager 찾기 (싱글톤이 아닐 경우를 대비해 Find 사용)
        var stepManager = FindAnyObjectByType<GameStepManager>();

        if (stepManager != null)
        {
            if (isGoal)
            {
                // 목표 지점 도달: 다음 단계 진행 요청
                Debug.Log($"[ZoneTrigger] Goal Reached: {gameObject.name}");
                stepManager.SetZoneReached(true);
            }
            else if (isDanger)
            {
                // 위험 구역 진입: 실수 카운트 증가 및 위치 리셋
                Debug.Log($"[ZoneTrigger] Danger Zone Entered: {gameObject.name}");

                if (DataManager.Instance != null)
                {
                    DataManager.Instance.AddMistakeCount();
                }

                stepManager.ReturnToSavedPosition();
            }
        }
        else
        {
            Debug.LogWarning("[ZoneTrigger] GameStepManager not found in scene. Trigger ignored.");
        }
    }

    #endregion
}