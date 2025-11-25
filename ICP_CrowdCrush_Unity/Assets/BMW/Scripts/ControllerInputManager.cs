using UnityEngine;
using UnityEngine.InputSystem;
using System;

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
    public Vector2 RightJoystickValue { get; private set; }

    // A,B 버튼 입력 이벤트 (구독 가능)
    public event Action OnAButtonDown;
    public event Action OnBButtonDown;
    public event Action OnYButtonDown;

    // 액션 참조 변수들
    private InputAction AButton, BButton, XButton, YButton;
    private InputAction RGripButton, LGripButton;
    private InputAction RTriggerButton, LTriggerButton;
    private InputAction RJoystick;

    private void Awake()
    {
        // 싱글톤 패턴 적용: 중복 생성 방지 및 씬 전환 시 유지
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

        var rLocoMap = inputActions.FindActionMap("XRI Right Locomotion");
        if (rLocoMap != null)
        {
            RJoystick = rLocoMap.FindAction("Turn");
            if (RJoystick != null)
            {
                RJoystick.Enable();
                RJoystick.performed += ctx => RightJoystickValue = ctx.ReadValue<Vector2>();
                RJoystick.canceled += ctx => RightJoystickValue = Vector2.zero;
            }

        }
    }

    // --- 기본 버튼 이벤트 핸들러 (로그 출력용) ---
    private void OnAButtonPressed(InputAction.CallbackContext ctx)
    {
        if (isDebug) Debug.Log("A Button Pressed (Action Triggered)");
        OnAButtonDown?.Invoke(); // 구독자들에게 알림
    }
    private void OnBButtonPressed(InputAction.CallbackContext ctx) {
        if (isDebug) Debug.Log("B Button Pressed (Action Triggered)");
        OnBButtonDown?.Invoke();
    }
    private void OnXButtonPressed(InputAction.CallbackContext ctx) { if (isDebug) Debug.Log("X Button Pressed"); }
    private void OnYButtonPressed(InputAction.CallbackContext ctx)
    {
        if (isDebug) Debug.Log("Y Button Pressed");
        OnYButtonDown?.Invoke();
    }

    private void OnDestroy()
    {
        if (inputActions != null) inputActions.Disable();
        // C# 이벤트 델리게이트는 오브젝트 파괴 시 가비지 컬렉터에 의해 정리되므로 명시적 해지 생략 가능
    }
}