using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [플레이어 이동 제어 매니저
/// - 외부에서 SetLocomotion(bool)을 호출하여 이동 기능을 켜고 끌 수 있음
/// - 씬 로드 시 자동으로 XR Origin을 찾아 초기화함
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Target Object Names (Partial Match)")]
    [SerializeField] private string originKeyword = "XROrigin";
    [SerializeField] private string locomotionKeyword = "Locomotion";
    [SerializeField] private string[] moveKeywords = { "Move", "Turn", "Teleport" };

    // 현재 씬의 XR Origin을 기억하기 위한 변수 (매번 찾지 않도록 최적화)
    private GameObject currentXROrigin;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 부모가 있다면 연결을 끊고 최상위 루트로 이동 (DontDestroyOnLoad 작동 보장)
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
            Debug.LogWarning($"[PlayerManage] '{scene.name}' 씬에서 '{originKeyword}'을 찾을 수 없습니다.");
            return;
        }

        // 기본 설정: 인트로 씬에서는 이동 금지, 그 외에는 허용
        bool allowLocomotion = !scene.name.Equals("IntroScene", System.StringComparison.OrdinalIgnoreCase);
        ControlLocomotion(currentXROrigin, allowLocomotion);
        Debug.Log($"[PlayerManager] Scene: {scene.name}, Locomotion Allowed: {allowLocomotion}");
    }

    // 외부에서 플레이어의 이동 기능을 끄거나 켤 때 호출하는 함수

    public void SetLocomotion(bool isEnabled)
    {
        // 만약 저장된 참조가 없으면 다시 찾기 시도
        if (currentXROrigin == null) currentXROrigin = FindXROrigin();

        if (currentXROrigin != null)
        {
            ControlLocomotion(currentXROrigin, isEnabled);
        }
        else
        {
            Debug.LogWarning("[PlayerManager] XR Origin을 찾을 수 없어 이동 제어에 실패했습니다.");
        }
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
        else
        {
            Debug.LogWarning($"[PlayerManager] '{xrOrigin.name}' 하위에서 '{locomotionKeyword}' 객체를 찾을 수 없습니다.");
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
}