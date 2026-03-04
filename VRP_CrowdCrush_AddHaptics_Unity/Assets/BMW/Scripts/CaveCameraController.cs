using UnityEngine;
using static DisplayModeManager;

public class CaveCameraController : MonoBehaviour
{
    // 어디서든 접근 가능한 싱글톤
    public static CaveCameraController Instance;

    [Header("카메라 직접 할당")]
    [Tooltip("인스펙터에서 Camera - Front를 직접 넣어주세요.")]
    public Camera frontCamera;

    [Header("Cave 카메라 그룹 부모")]
    [SerializeField] private GameObject caveCameraRoot;

    private void Awake()
    {
        // 싱글톤 등록
        Instance = this;

        // 만약 인스펙터에서 할당을 깜빡했다면 자동으로 찾기
        if (frontCamera == null && caveCameraRoot != null)
        {
            // 부모 아래에서 이름으로 찾기 (안전장치)
            var camObj = caveCameraRoot.transform.Find("Camera - Front");
            if (camObj != null) frontCamera = camObj.GetComponent<Camera>();
        }
    }

    private void Start()
    {
        // 안전장치
        if (caveCameraRoot == null) caveCameraRoot = gameObject;

        // 매니저 연결
        if (DisplayModeManager.Instance != null)
        {
            UpdateCaveState(DisplayModeManager.Instance.CurrentDisplayMode);
            DisplayModeManager.Instance.OnDisplayModeChanged += UpdateCaveState;
        }
        else
        {
            // 매니저 없으면 기본 끄기
            caveCameraRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (DisplayModeManager.Instance != null)
        {
            DisplayModeManager.Instance.OnDisplayModeChanged -= UpdateCaveState;
        }
    }

    private void UpdateCaveState(DisplayMode mode)
    {
        if (mode == DisplayMode.Cave)
        {
            if (!caveCameraRoot.activeSelf) caveCameraRoot.SetActive(true);
        }
        else
        {
            if (caveCameraRoot.activeSelf) caveCameraRoot.SetActive(false);
        }
    }
}