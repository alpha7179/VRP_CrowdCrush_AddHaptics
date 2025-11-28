using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic; // 리스트 사용을 위해 추가

/// <summary>
/// [플레이어 기능 제어 매니저]
/// - SetLocomotion(bool): 이동(Move, Turn, Teleport) 기능 제어
/// - SetInteraction(bool): 상호작용(Ray, Direct, Grab) 기능 제어
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Target Object Names (Partial Match)")]
    [SerializeField] private string originKeyword = "XROrigin";

    [Header("Locomotion Settings")]
    [SerializeField] private string locomotionKeyword = "Locomotion";
    [SerializeField] private string[] moveKeywords = { "Move", "Turn", "Teleport" };

    [Header("Interaction Settings")]
    [SerializeField]
    private string[] interactionKeywords = { "Direct Interactor", "UI&Teleport Ray Interactor" };

    // 현재 씬의 XR Origin을 기억하기 위한 변수
    private GameObject currentXROrigin;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 로드 시 플레이어 찾아서 저장
        currentXROrigin = FindXROrigin();

        if (currentXROrigin == null)
        {
            Debug.LogWarning($"[PlayerManager] '{scene.name}' 씬에서 '{originKeyword}'을 찾을 수 없습니다.");
            return;
        }

        // [핵심 수정] 씬 이름에 따라 초기 권한을 다르게 부여
        if (scene.name.Equals("IntroScene", System.StringComparison.OrdinalIgnoreCase))
        {
            // 1. 인트로 씬: UI 조작은 해야 하니 Interaction은 켜고, 돌아다니면 안 되니 Locomotion은 끔
            SetInteraction(true);  // ★ UI 클릭 허용
            SetLocomotion(false); // 이동 불가 (메뉴 앞 고정)
            Debug.Log("[PlayerManager] Intro Scene: Interaction ON / Locomotion OFF");
        }
        else
        {
            // 2. 시뮬레이션 씬 (그 외):
            // GameStepManager가 시작되자마자 시나리오에 맞춰 제어할 것이므로
            // 로딩 직후엔 '모두 잠금' 상태로 시작해서 오작동 방지
            SetInteraction(false);
            SetLocomotion(false);
            Debug.Log("[PlayerManager] Game Scene: All Features Locked (Waiting for GameStepManager)");
        }
    }

    // =================================================================================
    // 공개 제어 메서드
    // =================================================================================

    // 1. 이동 기능 제어
    public void SetLocomotion(bool isEnabled)
    {
        if (EnsureOriginFound())
        {
            ControlLocomotion(currentXROrigin, isEnabled);
        }
    }

    // 2. [추가] 상호작용 기능 제어
    public void SetInteraction(bool isEnabled)
    {
        if (EnsureOriginFound())
        {
            // 상호작용은 왼손/오른손에 나뉘어 있으므로 전체 검색으로 제어합니다.
            ControlFeaturesByKeywords(currentXROrigin, interactionKeywords, isEnabled);
        }
    }

    // =================================================================================
    // 내부 로직
    // =================================================================================

    private bool EnsureOriginFound()
    {
        if (currentXROrigin == null) currentXROrigin = FindXROrigin();

        if (currentXROrigin == null)
        {
            Debug.LogWarning("[PlayerManager] XR Origin을 찾을 수 없습니다.");
            return false;
        }
        return true;
    }

    private GameObject FindXROrigin()
    {
        var rootObjs = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var obj in rootObjs)
        {
            if (obj.name.StartsWith(originKeyword, System.StringComparison.OrdinalIgnoreCase))
            {
                return obj;
            }
        }
        return null;
    }

    // 이동 제어 (기존 로직 유지 - Locomotion 부모 아래에서 찾기)
    private void ControlLocomotion(GameObject xrOrigin, bool isEnabled)
    {
        Transform locomotionTr = FindChildRecursive(xrOrigin.transform, locomotionKeyword);

        if (locomotionTr != null)
        {
            foreach (string keyword in moveKeywords)
            {
                Transform targetTr = FindChildRecursive(locomotionTr, keyword);
                if (targetTr != null)
                {
                    targetTr.gameObject.SetActive(isEnabled);
                }
            }
        }
    }

    private void ControlFeaturesByKeywords(GameObject root, string[] keywords, bool isEnabled)
    {
        // 비활성화된 자식까지 모두 포함해서 검색 (true 파라미터)
        Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            foreach (string keyword in keywords)
            {
                // 이름에 키워드가 포함되어 있는지 확인 (대소문자 무시)
                if (child.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    child.gameObject.SetActive(isEnabled);
                }
            }
        }
    }

    // 단일 자식 찾기 (기존 로직)
    private Transform FindChildRecursive(Transform parent, string namePart)
    {
        foreach (Transform child in parent)
        {
            if (child.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }

            Transform result = FindChildRecursive(child, namePart);
            if (result != null) return result;
        }
        return null;
    }
}