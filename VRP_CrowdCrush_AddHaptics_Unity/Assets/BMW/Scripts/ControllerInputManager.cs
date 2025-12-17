using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Unity New Input System을 기반으로 XR 컨트롤러의 입력을 중앙에서 관리하는 매니저입니다.
/// <para>
/// 1. InputActionAsset을 로드하여 XR 컨트롤러의 버튼(A, B, X, Y), 그립, 트리거, 조이스틱 입력을 감지합니다.<br/>
/// 2. 입력 상태를 bool 프로퍼티로 제공하거나(Polling), 특정 버튼 클릭 시 이벤트를 발생(Event)시킵니다.<br/>
/// 3. 다른 스크립트에서 이 매니저를 통해 입력을 쉽게 참조할 수 있습니다.
/// </para>
/// </summary>
public class ControllerInputManager : MonoBehaviour
{
    #region Singleton

    public static ControllerInputManager Instance { get; private set; }

    private void Awake()
    {
        // 싱글톤 패턴: 중복 생성 방지 및 씬 전환 시 유지
        if (Instance == null)
        {
            Instance = this;
            transform.parent = null; // 최상위 계층으로 분리
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Inspector Settings

    [Header("Input Settings")]
    [Tooltip("XR Interaction Toolkit의 기본 Input Action Asset을 할당하세요.")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Debug")]
    [SerializeField] private bool isDebug = true;

    #endregion

    #region Input State (Public Properties)

    // --- Grip & Trigger States ---
    /// <summary>오른손 그립 버튼이 눌려있는지 여부</summary>
    public bool IsRightGripHeld { get; private set; }
    /// <summary>왼손 그립 버튼이 눌려있는지 여부</summary>
    public bool IsLeftGripHeld { get; private set; }
    /// <summary>오른손 트리거(검지) 버튼이 눌려있는지 여부</summary>
    public bool IsRightTriggerHeld { get; private set; }
    /// <summary>왼손 트리거(검지) 버튼이 눌려있는지 여부</summary>
    public bool IsLeftTriggerHeld { get; private set; }

    // --- Joystick States ---
    /// <summary>오른손 조이스틱의 입력값 (Vector2)</summary>
    public Vector2 RightJoystickValue { get; private set; }

    #endregion

    #region Events

    // --- Button Click Events ---
    /// <summary>오른손 A 버튼을 눌렀을 때 발생</summary>
    public event Action OnAButtonDown;
    /// <summary>오른손 B 버튼을 눌렀을 때 발생</summary>
    public event Action OnBButtonDown;
    /// <summary>왼손 Y 버튼을 눌렀을 때 발생</summary>
    public event Action OnYButtonDown;

    #endregion

    #region Internal Action References

    // Action 참조 변수들 (직접 참조하여 Enable/Disable 관리)
    private InputAction AButton, BButton, XButton, YButton;
    private InputAction RGripButton, LGripButton;
    private InputAction RTriggerButton, LTriggerButton;
    private InputAction RJoystick;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        SetupInputActions();
    }

    private void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
        }
        // C# 이벤트(Action)는 객체 파괴 시 GC가 처리하므로 명시적 null 할당은 필수가 아님
    }

    #endregion

    #region Initialization Logic

    /// <summary>
    /// Input Action Asset에서 필요한 액션 맵(Map)과 액션(Action)을 찾아 바인딩하고 활성화합니다.
    /// </summary>
    private void SetupInputActions()
    {
        if (inputActions == null)
        {
            if (isDebug) Debug.LogError("[ControllerInputManager] InputActionAsset is missing!");
            return;
        }

        // 전체 액션 활성화
        inputActions.Enable();

        // 1. Right Controller Maps (Primary Buttons)
        var rightMap = inputActions.FindActionMap("XRI Right");
        if (rightMap != null)
        {
            AButton = rightMap.FindAction("AButton");
            if (AButton != null) { AButton.Enable(); AButton.performed += OnAButtonPressed; }

            BButton = rightMap.FindAction("BButton");
            if (BButton != null) { BButton.Enable(); BButton.performed += OnBButtonPressed; }
        }

        // 2. Left Controller Maps (Primary Buttons)
        var leftMap = inputActions.FindActionMap("XRI Left");
        if (leftMap != null)
        {
            XButton = leftMap.FindAction("XButton");
            if (XButton != null) { XButton.Enable(); XButton.performed += OnXButtonPressed; }

            YButton = leftMap.FindAction("YButton");
            if (YButton != null) { YButton.Enable(); YButton.performed += OnYButtonPressed; }
        }

        // 3. Right Interaction (Grip / Trigger)
        var rInteractMap = inputActions.FindActionMap("XRI Right Interaction");
        if (rInteractMap != null)
        {
            // Grip (Select)
            RGripButton = rInteractMap.FindAction("Select");
            if (RGripButton != null)
            {
                RGripButton.Enable();
                RGripButton.performed += ctx => { IsRightGripHeld = true; if (isDebug) Debug.Log("R Grip Held"); };
                RGripButton.canceled += ctx => { IsRightGripHeld = false; if (isDebug) Debug.Log("R Grip Released"); };
            }

            // Trigger (Activate)
            RTriggerButton = rInteractMap.FindAction("Activate");
            if (RTriggerButton != null)
            {
                RTriggerButton.Enable();
                RTriggerButton.performed += ctx => { IsRightTriggerHeld = true; if (isDebug) Debug.Log("R Trigger Held"); };
                RTriggerButton.canceled += ctx => { IsRightTriggerHeld = false; if (isDebug) Debug.Log("R Trigger Released"); };
            }
        }

        // 4. Left Interaction (Grip / Trigger)
        var lInteractMap = inputActions.FindActionMap("XRI Left Interaction");
        if (lInteractMap != null)
        {
            // Grip (Select)
            LGripButton = lInteractMap.FindAction("Select");
            if (LGripButton != null)
            {
                LGripButton.Enable();
                LGripButton.performed += ctx => { IsLeftGripHeld = true; if (isDebug) Debug.Log("L Grip Held"); };
                LGripButton.canceled += ctx => { IsLeftGripHeld = false; if (isDebug) Debug.Log("L Grip Released"); };
            }

            // Trigger (Activate)
            LTriggerButton = lInteractMap.FindAction("Activate");
            if (LTriggerButton != null)
            {
                LTriggerButton.Enable();
                LTriggerButton.performed += ctx => { IsLeftTriggerHeld = true; if (isDebug) Debug.Log("L Trigger Held"); };
                LTriggerButton.canceled += ctx => { IsLeftTriggerHeld = false; if (isDebug) Debug.Log("L Trigger Released"); };
            }
        }

        // 5. Locomotion (Joystick)
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

    #endregion

    #region Event Handlers

    private void OnAButtonPressed(InputAction.CallbackContext ctx)
    {
        if (isDebug) Debug.Log("A Button Pressed");
        OnAButtonDown?.Invoke();
    }

    private void OnBButtonPressed(InputAction.CallbackContext ctx)
    {
        if (isDebug) Debug.Log("B Button Pressed");
        OnBButtonDown?.Invoke();
    }

    private void OnXButtonPressed(InputAction.CallbackContext ctx)
    {
        if (isDebug) Debug.Log("X Button Pressed");
        // X버튼 이벤트는 필요 시 추가 (현재는 로그만)
    }

    private void OnYButtonPressed(InputAction.CallbackContext ctx)
    {
        if (isDebug) Debug.Log("Y Button Pressed");
        OnYButtonDown?.Invoke();
    }

    #endregion
}