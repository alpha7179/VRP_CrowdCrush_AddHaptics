using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// XR 컨트롤러 입력을 처리하는 싱글톤 매니저
/// </summary>
public class ControllerInputManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static ControllerInputManager Instance { get; private set; }

    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Debug")]
    [SerializeField] private bool isDebug = true;

    // 외부 접근 프로퍼티 (다른 스크립트에서 입력 상태 확인용)
    public bool IsRightGripHeld { get; private set; }
    public bool IsLeftGripHeld { get; private set; }
    public bool IsRightTriggerHeld { get; private set; }
    public bool IsLeftTriggerHeld { get; private set; }

    // 액션 참조 변수들
    private InputAction AButton, BButton, XButton, YButton;
    private InputAction RGripButton, LGripButton;
    private InputAction RTriggerButton, LTriggerButton;
    private InputAction MenuButton; // 추가된 메뉴 버튼

    private void Awake()
    {
        // 싱글톤 패턴 적용: 중복 생성 방지 및 씬 전환 시 유지
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

    void Start()
    {
        SetupInputActions();
    }

    /// <summary>
    /// Input Action Asset에서 액션을 찾아 바인딩하고 활성화합니다.
    /// </summary>
    private void SetupInputActions()
    {
        if (inputActions == null)
        {
            if (isDebug) Debug.LogError("InputActionAsset not found!");
            return;
        }

        inputActions.Enable();

        // --- Right Controller (A, B 버튼) ---
        var rightMap = inputActions.FindActionMap("XRI Right");
        if (rightMap != null)
        {
            AButton = rightMap.FindAction("AButton");
            if (AButton != null) { AButton.Enable(); AButton.performed += OnAButtonPressed; }

            BButton = rightMap.FindAction("BButton");
            if (BButton != null) { BButton.Enable(); BButton.performed += OnBButtonPressed; }
        }

        // --- Left Controller (X, Y, Menu 버튼) ---
        var leftMap = inputActions.FindActionMap("XRI Left");
        if (leftMap != null)
        {
            XButton = leftMap.FindAction("XButton");
            if (XButton != null) { XButton.Enable(); XButton.performed += OnXButtonPressed; }

            YButton = leftMap.FindAction("YButton");
            if (YButton != null) { YButton.Enable(); YButton.performed += OnYButtonPressed; }

            // 메뉴 버튼 바인딩
            MenuButton = leftMap.FindAction("YButton");
            if (MenuButton != null)
            {
                MenuButton.Enable();
                MenuButton.performed += OnMenuButtonPressed;
            }
            else
            {
                if (isDebug) Debug.LogWarning("Menu Action not found in XRI Left map. Check Action Name.");
            }
        }

        // --- Interaction (Grip/Trigger) ---
        var rInteractMap = inputActions.FindActionMap("XRI Right Interaction");
        if (rInteractMap != null)
        {
            RGripButton = rInteractMap.FindAction("Select");
            if (RGripButton != null)
            {
                RGripButton.Enable();
                RGripButton.performed += ctx => { IsRightGripHeld = true; if (isDebug) Debug.Log("R Grip Held"); };
                RGripButton.canceled += ctx => { IsRightGripHeld = false; if (isDebug) Debug.Log("R Grip Released"); };
            }

            RTriggerButton = rInteractMap.FindAction("Activate");
            if (RTriggerButton != null)
            {
                RTriggerButton.Enable();
                RTriggerButton.performed += ctx => { IsRightTriggerHeld = true; if (isDebug) Debug.Log("R Trigger Held"); };
                RTriggerButton.canceled += ctx => { IsRightTriggerHeld = false; if (isDebug) Debug.Log("R Trigger Released"); };
            }
        }

        var lInteractMap = inputActions.FindActionMap("XRI Left Interaction");
        if (lInteractMap != null)
        {
            LGripButton = lInteractMap.FindAction("Select");
            if (LGripButton != null)
            {
                LGripButton.Enable();
                LGripButton.performed += ctx => { IsLeftGripHeld = true; if (isDebug) Debug.Log("L Grip Held"); };
                LGripButton.canceled += ctx => { IsLeftGripHeld = false; if (isDebug) Debug.Log("L Grip Released"); };
            }

            LTriggerButton = lInteractMap.FindAction("Activate");
            if (LTriggerButton != null)
            {
                LTriggerButton.Enable();
                LTriggerButton.performed += ctx => { IsLeftTriggerHeld = true; if (isDebug) Debug.Log("L Trigger Held"); };
                LTriggerButton.canceled += ctx => { IsLeftTriggerHeld = false; if (isDebug) Debug.Log("L Trigger Released"); };
            }
        }
    }

    // --- 기본 버튼 이벤트 핸들러 (로그 출력용) ---
    private void OnAButtonPressed(InputAction.CallbackContext ctx) { if (isDebug) Debug.Log("A Pressed"); }
    private void OnBButtonPressed(InputAction.CallbackContext ctx) { if (isDebug) Debug.Log("B Pressed"); }
    private void OnXButtonPressed(InputAction.CallbackContext ctx) { if (isDebug) Debug.Log("X Pressed"); }
    private void OnYButtonPressed(InputAction.CallbackContext ctx) { if (isDebug) Debug.Log("Y Pressed"); }

    // [수정됨] 메뉴 버튼 -> GameManager 일시정지 호출 (조건부 실행)
    private void OnMenuButtonPressed(InputAction.CallbackContext ctx)
    {
        if (isDebug) Debug.Log("Menu Button Input Received");

        // 1. GameManager 인스턴스 확인
        if (GameManager.Instance == null) return;

        // 2. 인트로 씬인지 확인 (인트로에서는 메뉴 버튼 무시)
        // GameManager3를 쓰고 있다면 GameManager3.Instance를 참조해야 할 수도 있음 (여기서는 GameManager로 통일)
        if (GameManager.Instance.CurrentSceneName.Equals("IntroScene", System.StringComparison.OrdinalIgnoreCase))
        {
            if (isDebug) Debug.Log("Ignored: In Intro Scene");
            return;
        }

        // 3. 아웃트로 UI(결과창)가 떠있는지 확인
        // FindObjectOfType은 무거울 수 있으니, 실제로는 GameManager나 UIManager를 통해 상태를 받아오는 것이 좋음.
        // 여기서는 안전하고 확실한 방법을 위해 씬 내 활성화된 OuttroUIManager를 찾음.
        var outtroUI = FindAnyObjectByType<OuttroUIManager>();
        if (outtroUI != null && outtroUI.gameObject.activeInHierarchy)
        {
            if (isDebug) Debug.Log("Ignored: Outtro UI is Active");
            return;
        }

        // 모든 조건을 통과했을 때만 일시정지 토글
        GameManager.Instance.TogglePause();
    }

    private void OnDestroy()
    {
        if (inputActions != null) inputActions.Disable();
        // C# 이벤트 델리게이트는 오브젝트 파괴 시 가비지 컬렉터에 의해 정리되므로 명시적 해지 생략 가능
    }
}