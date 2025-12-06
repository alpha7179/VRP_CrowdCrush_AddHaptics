using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// 플레이어의 이동(Locomotion) 및 상호작용(Interaction) 기능을 중앙에서 관리하는 매니저입니다.
/// <para>
/// 1. 씬 로드 시 XR Origin을 자동으로 탐색하여 참조를 갱신합니다.<br/>
/// 2. 씬의 종류(Intro vs Game)에 따라 초기 권한을 자동으로 설정합니다.<br/>
/// 3. 외부(GameStepManager 등)에서 플레이어의 기능을 제어할 수 있는 API를 제공합니다.
/// </para>
/// </summary>
public class PlayerManager : MonoBehaviour
{
    #region Singleton

    public static PlayerManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.parent = null; // 최상위 계층으로 분리하여 관리
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

    }

    #endregion

    #region Inspector Settings

    [Header("Target Object Names")]
    [Tooltip("플레이어의 최상위 부모 객체 이름 (XR Origin 검색용 키워드)")]
    [SerializeField] private string originKeyword = "XROrigin";

    [Header("Locomotion Settings")]
    [Tooltip("이동 시스템 그룹 객체의 이름 (Locomotion System)")]
    [SerializeField] private string locomotionKeyword = "Locomotion";

    [Tooltip("제어할 이동 관련 컴포넌트 또는 자식 객체의 키워드 목록 (Move, Turn, Teleport 등)")]
    [SerializeField] private string[] moveKeywords = { "Turn", "Teleport", "Move" };

    [Header("Interaction Settings")]
    [Tooltip("제어할 상호작용 관련 컴포넌트 또는 자식 객체의 키워드 목록 (Ray Interactor, Direct Interactor 등)")]
    [SerializeField] private string[] interactionKeywords = { "Direct Interactor", "UI&Teleport Ray Interactor" };

    #endregion

    #region Internal State

    /// <summary>
    /// 현재 활성화된 씬의 XR Origin 참조입니다. 씬이 바뀔 때마다 갱신됩니다.
    /// </summary>
    private GameObject currentXROrigin;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 씬 로드가 완료될 때 호출되어 플레이어를 찾고 초기 권한을 설정합니다.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. 현재 씬의 XR Origin 탐색
        currentXROrigin = FindXROrigin();

        if (currentXROrigin == null)
        {
            Debug.LogWarning($"[PlayerManager] '{scene.name}' 씬에서 '{originKeyword}' 객체를 찾을 수 없습니다.");
            return;
        }

        // 2. 씬 타입에 따른 초기 권한 설정
        // 비교 시 대소문자를 무시하여 안전하게 체크 (OrdinalIgnoreCase)
        if(scene.name.Equals("Main_Street", System.StringComparison.OrdinalIgnoreCase))
        {
            // Game 씬 (시뮬레이션):
            // 시나리오 매니저(GameStepManager)가 제어권을 가질 때까지 오작동 방지를 위해 모든 기능 잠금
            SetInteraction(false);
            SetLocomotion(false);
            Debug.Log("[PlayerManager] Game Scene: All Features Locked (Waiting for GameStepManager)");
        }
        else
        {
            // Intro 씬: 메뉴 조작(Interaction)은 필요하지만, 이동(Locomotion)은 제한
            SetInteraction(true);
            SetLocomotion(false);
            Debug.Log("[PlayerManager] Intro Scene: Interaction ON / Locomotion OFF");
        }
        
    }

    #endregion

    #region Public API

    /// <summary>
    /// 플레이어의 이동(Move, Turn, Teleport) 기능을 활성화하거나 비활성화합니다.
    /// </summary>
    /// <param name="isEnabled">true: 이동 가능, false: 이동 불가</param>
    public void SetLocomotion(bool isEnabled)
    {
        if (EnsureOriginFound())
        {
            ControlLocomotion(currentXROrigin, isEnabled);
        }
    }

    /// <summary>
    /// 플레이어의 상호작용(Ray, Direct Grab 등) 기능을 활성화하거나 비활성화합니다.
    /// </summary>
    /// <param name="isEnabled">true: 상호작용 가능, false: 상호작용 불가</param>
    public void SetInteraction(bool isEnabled)
    {
        if (EnsureOriginFound())
        {
            // 상호작용 컴포넌트는 컨트롤러 하위 여러 곳에 분산되어 있을 수 있으므로 전체 검색으로 제어
            ControlFeaturesByKeywords(currentXROrigin, interactionKeywords, isEnabled);
        }
    }

    #endregion

    #region Helper Methods (Core Logic)

    /// <summary>
    /// XR Origin 참조가 유효한지 확인하고, 없다면 재탐색을 시도합니다.
    /// </summary>
    /// <returns>참조가 유효하면 true, 아니면 false</returns>
    private bool EnsureOriginFound()
    {
        if (currentXROrigin == null) currentXROrigin = FindXROrigin();

        if (currentXROrigin == null)
        {
            Debug.LogWarning("[PlayerManager] XR Origin을 찾을 수 없어 명령을 수행할 수 없습니다.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 현재 씬의 루트 객체들 중에서 지정된 키워드(XR Origin)를 가진 객체를 찾습니다.
    /// </summary>
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

    /// <summary>
    /// Locomotion 시스템 하위의 특정 기능(Move, Turn 등)을 제어합니다.
    /// </summary>
    private void ControlLocomotion(GameObject xrOrigin, bool isEnabled)
    {
        // 1. Locomotion System 부모 찾기
        Transform locomotionTr = FindChildRecursive(xrOrigin.transform, locomotionKeyword);

        if (locomotionTr != null)
        {
            // 2. 하위의 이동 관련 객체(Move, Turn, Teleport)들을 찾아 켜거나 끔
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

    /// <summary>
    /// 루트 객체 하위의 모든 자식 중에서 키워드가 포함된 객체들을 찾아 활성화/비활성화합니다.
    /// (상호작용 기능 제어용: 컨트롤러 구조가 복잡할 수 있어 전체 검색 사용)
    /// </summary>
    private void ControlFeaturesByKeywords(GameObject root, string[] keywords, bool isEnabled)
    {
        // 비활성화된 자식(true)까지 모두 포함해서 검색해야 꺼진 기능을 다시 켤 수 있음
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

    /// <summary>
    /// 재귀적으로 자식 객체를 탐색하여 특정 이름을 가진 첫 번째 객체를 반환합니다.
    /// </summary>
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

    #endregion
}