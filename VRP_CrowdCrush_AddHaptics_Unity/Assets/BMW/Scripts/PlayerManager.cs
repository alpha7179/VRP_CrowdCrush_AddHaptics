using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
// [중요] XR Interaction Toolkit 네임스페이스 추가
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

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

    // [변경] 이름 검색 방식 제거 -> 키워드 설정 불필요
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
        currentXROrigin = FindXROrigin();

        if (currentXROrigin == null)
        {
            Debug.LogWarning($"[PlayerManager] '{scene.name}' 씬에서 '{originKeyword}' 객체를 찾을 수 없습니다.");
            return;
        }

        // 초기 설정: 일단 다 끄고 시작 (GameStepManager가 켜줄 것임)
        if (scene.name.Equals("Main_Street", System.StringComparison.OrdinalIgnoreCase))
        {
            SetInteraction(false);
            SetLocomotion(false);
            Debug.Log("[PlayerManager] Game Scene: Features Locked");
        }
        else
        {
            SetInteraction(true);
            SetLocomotion(true); // 인트로 등에서는 이동 허용
        }

        bool comfortMode = (DataManager.Instance != null) && DataManager.Instance.IsAntiMotionSicknessMode;
        SetComfortMode(comfortMode);
    }
    #endregion

    #region Public API

    /// <summary>
    /// 이동(Move, Turn, Teleport) 기능을 켜거나 끕니다.
    /// [수정] 컴포넌트를 직접 찾아 제어하므로 이름이 달라도 작동합니다.
    /// </summary>
    public void SetLocomotion(bool isEnabled)
    {
        if (!EnsureOriginFound()) return;

        // LocomotionProvider는 Move, Turn, Teleport 등의 부모 클래스입니다.
        // 이것들을 다 찾아서 끄면 이동/회전이 멈춥니다.
        var providers = currentXROrigin.GetComponentsInChildren<LocomotionProvider>(true);

        foreach (var provider in providers)
        {
            provider.enabled = isEnabled;
        }

        Debug.Log($"[PlayerManager] Locomotion set to: {isEnabled} (Controlled {providers.Length} providers)");
    }

    /// <summary>
    /// 상호작용(Ray, Direct Interactor) 기능을 켜거나 끕니다.
    /// </summary>
    public void SetInteraction(bool isEnabled)
    {
        if (!EnsureOriginFound()) return;

        // XRRayInteractor, XRDirectInteractor 등을 찾아서 제어
        // (XRBaseInteractor는 모든 상호작용의 부모)
        // 주의: Locomotion도 Interactor를 쓸 수 있으므로, 손(Hand)에 있는 것만 끄는 것이 좋으나
        // 여기서는 간단히 Interactor 전체를 제어합니다.

        // [팁] 만약 텔레포트 Ray까지 꺼지면 안 된다면 태그나 레이어 필터링이 필요할 수 있습니다.
        // 여기서는 기존 로직대로 '이름'에 Interactor가 포함된 것들을 찾거나, 컴포넌트로 제어합니다.

        // 이름 기반 검색 (기존 유지 - 상호작용은 보통 손에 달려있어서 이름이 명확함)
        string[] keywords = { "Interactor" };
        Transform[] allChildren = currentXROrigin.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            // Locomotion 관련 Interactor는 끄지 않도록 예외 처리 가능
            if (child.name.Contains("Locomotion") || child.name.Contains("Teleport")) continue;

            if (child.name.Contains("Interactor"))
            {
                child.gameObject.SetActive(isEnabled);
            }
        }
    }

    public void SetComfortMode(bool isEnabled)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null && EnsureOriginFound()) mainCam = currentXROrigin.GetComponentInChildren<Camera>();

        if (mainCam == null) return;

        Transform vignetteTr = FindChildRecursive(mainCam.transform, vignetteKeyword);
        if (vignetteTr != null)
        {
            vignetteTr.gameObject.SetActive(isEnabled);
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
        // 태그로 찾기 시도 (차선책)
        GameObject tagObj = GameObject.FindGameObjectWithTag("Player");
        if (tagObj != null) return tagObj;

        return null;
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