using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 이동 제어 매니저
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Target Object Names (Partial Match)")]
    [SerializeField] private string originKeyword = "XROrigin";
    [SerializeField] private string locomotionKeyword = "Locomotion";
    [SerializeField] private string[] moveKeywords = { "Move", "Turn", "Teleport" };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        // GameManager 의존성을 줄이기 위해 직접 구독하거나 GameManager 경유
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. XR Origin 찾기 (이름으로 검색)
        GameObject xrOrigin = FindXROrigin();

        if (xrOrigin == null)
        {
            Debug.LogWarning($"[PlayerManager2] '{scene.name}' 씬에서 '{originKeyword}'을 찾을 수 없습니다.");
            return;
        }

        // 2. IntroScene이면 이동 제한, 아니면 허용
        bool allowLocomotion = !scene.name.Equals("IntroScene", System.StringComparison.OrdinalIgnoreCase);

        ControlLocomotion(xrOrigin, allowLocomotion);
        Debug.Log($"[PlayerManager2] Scene: {scene.name}, Locomotion Allowed: {allowLocomotion}");
    }

    // 씬 루트 객체 중에서 XROrigin 키워드로 시작하는 객체 검색 (대소문자 무시)
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

    // XROrigin -> Locomotion -> Move/Turn/Teleport 순으로 찾아 활성/비활성화
 
    private void ControlLocomotion(GameObject xrOrigin, bool isEnabled)
    {
        // 1. Locomotion 객체 찾기 (자식 전체 재귀 검색)
        Transform locomotionTr = FindChildRecursive(xrOrigin.transform, locomotionKeyword);

        if (locomotionTr != null)
        {
            // 2. 하위의 Move, Turn, Teleport 객체들을 찾아 끄거나 킴
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
            Debug.LogWarning($"[PlayerManager2] '{xrOrigin.name}' 하위에서 '{locomotionKeyword}' 객체를 찾을 수 없습니다.");
        }
    }

    // 이름에 특정 키워드가 포함된 자식을 재귀적으로 검색
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