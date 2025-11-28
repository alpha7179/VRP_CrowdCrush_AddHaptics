using UnityEngine;

/// <summary>
/// 플레이어가 특정 구역(Zone)에 진입했는지 감지하여 GameStepManager에 알리는 트리거.
/// Box Collider (Is Trigger 체크)가 있는 오브젝트에 추가하세요.
/// </summary>
public class ZoneTrigger : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool isGoal = true;   // 다음 단계로 넘어가는 목표 지점인가?
    [SerializeField] private bool isDanger = false; // 들어가면 안 되는 위험 지역인가?
    [SerializeField] private bool isDebug = true;

    [Header("Target Tag")]
    [SerializeField] private string playerTag = "Player"; // 플레이어 태그 확인 필수!

    private void OnTriggerEnter(Collider other)
    {
        // 플레이어인지 확인 (Tag 설정 또는 Root의 Tag 확인)
        // XR Origin은 구조상 Collider가 자식 객체에 있을 수 있으므로 root까지 확인
        if (other.CompareTag(playerTag) || (other.transform.root != null && other.transform.root.CompareTag(playerTag)))
        {
            if (isDebug) Debug.Log($"[ZoneTrigger] Player entered trigger: {gameObject.name}");

            // 씬에 있는 GameStepManager 찾기
            var stepManager = FindAnyObjectByType<GameStepManager>();

            if (stepManager != null)
            {
                if (isGoal)
                {
                    Debug.Log($"[ZoneTrigger] Goal Reached: {gameObject.name}");
                    stepManager.SetZoneReached(true);

                }
                else if (isDanger)
                {
                    Debug.Log($"[ZoneTrigger] Danger Zone Entered: {gameObject.name}");
                    DataManager.Instance.AddMistakeCount();
                    stepManager.ReturnToSavedPosition();

                }
            }
            else
            {
                Debug.LogWarning("[ZoneTrigger] GameStepManager not found in scene.");
            }
        }
    }
}