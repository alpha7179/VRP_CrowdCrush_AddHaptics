using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// 플레이어의 기능(이동, 상호작용) 및 편의 설정(멀미 모드)을 관리하는 매니저입니다.
/// <para>
/// 1. 씬 로드 시 XR Origin을 자동으로 탐색하여 참조를 갱신합니다.<br/>
/// 2. 씬의 종류(Intro vs Game)에 따라 초기 권한을 자동으로 설정합니다.<br/>
/// 3. DataManager와 연동하여 씬이 바뀔 때마다 비네팅(멀미 저감) 상태를 동기화합니다.<br/>
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

    // [추가] 멀미 방지 비네팅 오브젝트 이름
    [Tooltip("멀미 방지 비네팅 오브젝트 이름 (Main Camera의 자식이어야 함)")]
    [SerializeField] private string vignetteKeyword = "TunnelingVignette";

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

        // [추가] 런타임 중 설정 변경에도 반응하도록 이벤트 구독
        if (DataManager.Instance != null)
        {
            DataManager.Instance.OnMotionSicknessChanged += SetComfortMode;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // [추가] 이벤트 구독 해제
        if (DataManager.Instance != null)
        {
            DataManager.Instance.OnMotionSicknessChanged -= SetComfortMode;
        }
    }

    /// <summary>
    /// 씬 로드가 완료될 때 호출되어 플레이어를 찾고 초기 권한 및 설정을 적용합니다.
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
        if (scene.name.Equals("Main_Street", System.StringComparison.OrdinalIgnoreCase))
        {
            // Game 씬: 시나리오 매니저 대기 (기능 잠금)
            SetInteraction(false);
            SetLocomotion(false);
            Debug.Log("[PlayerManager] Game Scene: All Features Locked (Waiting for GameStepManager)");
        }
        else
        {
            // Intro 씬: 상호작용만 허용
            SetInteraction(true);
            SetLocomotion(false);
            Debug.Log("[PlayerManager] Intro Scene: Interaction ON / Locomotion OFF");
        }

        // 3. [추가] 멀미 방지 모드 상태 동기화 (새로 로드된 플레이어에게 적용)
        if (DataManager.Instance != null)
        {
            SetComfortMode(DataManager.Instance.IsAntiMotionSicknessMode);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// 플레이어의 이동(Move, Turn, Teleport) 기능을 활성화하거나 비활성화합니다.
    /// </summary>
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
    public void SetInteraction(bool isEnabled)
    {
        if (EnsureOriginFound())
        {
            ControlFeaturesByKeywords(currentXROrigin, interactionKeywords, isEnabled);
        }
    }

    /// <summary>
    /// [추가] 멀미 방지 모드(Tunneling Vignette)를 켜거나 끕니다.
    /// </summary>
    public void SetComfortMode(bool isEnabled)
    {
        if (!EnsureOriginFound()) return;

        // 1. 메인 카메라 찾기 (XR Origin의 자식 중 Camera 컴포넌트가 있는 객체)
        Camera mainCam = currentXROrigin.GetComponentInChildren<Camera>();
        if (mainCam == null)
        {
            Debug.LogWarning("[PlayerManager] Main Camera를 찾을 수 없습니다.");
            return;
        }

        // 2. 카메라 하위에서 Vignette 오브젝트 찾기
        Transform vignetteTr = FindChildRecursive(mainCam.transform, vignetteKeyword);

        if (vignetteTr != null)
        {
            vignetteTr.gameObject.SetActive(isEnabled);
            Debug.Log($"[PlayerManager] Comfort Mode Set: {isEnabled} (On Object: {vignetteTr.name})");
        }
        else
        {
            // 찾지 못했을 경우 (오브젝트 이름이나 구조 확인 필요)
            // Debug.LogWarning($"[PlayerManager] '{vignetteKeyword}' 객체를 카메라 하위에서 찾을 수 없습니다.");
        }
    }

    #endregion

    #region Helper Methods (Core Logic)

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
        Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            foreach (string keyword in keywords)
            {
                if (child.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    child.gameObject.SetActive(isEnabled);
                }
            }
        }
    }

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