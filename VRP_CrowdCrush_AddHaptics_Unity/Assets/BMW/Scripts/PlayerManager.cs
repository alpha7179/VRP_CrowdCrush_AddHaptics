using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// 플레이어의 기능(이동, 상호작용) 및 편의 설정(멀미 모드)을 관리하는 매니저입니다.
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
            transform.parent = null;
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

    [Tooltip("멀미 방지 비네팅 오브젝트 이름 (Main Camera의 자식이어야 함)")]
    [SerializeField] private string vignetteKeyword = "TunnelingVignette";

    [Header("Locomotion Settings")]
    [SerializeField] private string locomotionKeyword = "Locomotion";
    [SerializeField] private string[] moveKeywords = { "Turn", "Teleport", "Move" };

    [Header("Interaction Settings")]
    [SerializeField] private string[] interactionKeywords = { "Direct Interactor", "UI&Teleport Ray Interactor" };

    #endregion

    #region Internal State

    private GameObject currentXROrigin;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (DataManager.Instance != null) DataManager.Instance.OnMotionSicknessChanged += SetComfortMode;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (DataManager.Instance != null) DataManager.Instance.OnMotionSicknessChanged -= SetComfortMode;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. XR Origin 탐색
        currentXROrigin = FindXROrigin();

        if (currentXROrigin == null)
        {
            Debug.LogWarning($"[PlayerManager] '{scene.name}' 씬에서 '{originKeyword}' 객체를 찾을 수 없습니다.");
            return;
        }

        // 2. 초기 권한 설정
        if (scene.name.Equals("Main_Street", System.StringComparison.OrdinalIgnoreCase))
        {
            SetInteraction(false);
            SetLocomotion(false);
            Debug.Log("[PlayerManager] Game Scene: Features Locked");
        }
        else
        {
            SetInteraction(true);
            SetLocomotion(false);
            Debug.Log("[PlayerManager] Intro Scene: Interaction ON / Locomotion OFF");
        }

        // 3. 멀미 모드 강제 동기화
        bool comfortMode = false;
        if (DataManager.Instance != null)
        {
            comfortMode = DataManager.Instance.IsAntiMotionSicknessMode;
        }
        else
        {
            Debug.LogWarning("[PlayerManager] DataManager Instance not found. Defaulting Comfort Mode to FALSE.");
        }

        // 씬 로드 직후 안정적인 검색을 위해 약간 지연 후 실행 권장하지만, 일단 직접 실행
        SetComfortMode(comfortMode);
    }

    #endregion

    #region Public API

    public void SetLocomotion(bool isEnabled)
    {
        if (EnsureOriginFound()) ControlLocomotion(currentXROrigin, isEnabled);
    }

    public void SetInteraction(bool isEnabled)
    {
        if (EnsureOriginFound()) ControlFeaturesByKeywords(currentXROrigin, interactionKeywords, isEnabled);
    }

    /// <summary>
    /// 멀미 방지 모드(Tunneling Vignette)를 켜거나 끕니다.
    /// [수정됨] MainCamera 태그를 사용하여 더 확실하게 찾습니다.
    /// </summary>
    public void SetComfortMode(bool isEnabled)
    {
        // 1. 태그로 메인 카메라 찾기 (가장 확실한 방법)
        Camera mainCam = Camera.main;

        // 태그로 못 찾았으면 XR Origin 하위에서 검색 (차선책)
        if (mainCam == null && EnsureOriginFound())
        {
            mainCam = currentXROrigin.GetComponentInChildren<Camera>();
        }

        if (mainCam == null)
        {
            Debug.LogWarning("[PlayerManager] 씬에서 Main Camera를 찾을 수 없습니다. (MainCamera 태그를 확인하세요)");
            return;
        }

        // 2. 카메라 바로 아래 자식들 중에서 이름으로 찾기
        // (재귀 함수 대신 직접 자식을 순회하여 정확도 높임)
        Transform vignetteTr = null;
        foreach (Transform child in mainCam.transform)
        {
            if (child.name.Equals(vignetteKeyword))
            {
                vignetteTr = child;
                break;
            }
        }

        // 만약 바로 아래에 없다면 재귀 검색 시도
        if (vignetteTr == null)
        {
            vignetteTr = FindChildRecursive(mainCam.transform, vignetteKeyword);
        }

        if (vignetteTr != null)
        {
            vignetteTr.gameObject.SetActive(isEnabled);
            Debug.Log($"[PlayerManager] Vignette '{vignetteTr.name}' SetActive: {isEnabled}");
        }
        else
        {
            // 찾기 실패 시 계층 구조 로그 출력하여 디버깅 도움
            Debug.LogWarning($"[PlayerManager] '{vignetteKeyword}'를 '{mainCam.name}' 하위에서 찾을 수 없습니다.");
        }
    }

    #endregion

    #region Helper Methods

    private bool EnsureOriginFound()
    {
        if (currentXROrigin == null) currentXROrigin = FindXROrigin();
        return currentXROrigin != null;
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
                if (targetTr != null) targetTr.gameObject.SetActive(isEnabled);
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
            // [수정] 정확한 이름 일치를 우선 확인하도록 변경 가능하나, 기존 로직 유지하되 대소문자 무시
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