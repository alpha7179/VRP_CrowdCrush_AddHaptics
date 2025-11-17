using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ControllerInputManager : MonoBehaviour
{
    // inputActions 참조
    [Header("inputActions")]
    [SerializeField] private InputActionAsset inputActions; // 컨트롤러 매핑을 위한 Input Action Asset 참조

    // 디버그
    [Header("Debug Log")]
    [SerializeField] private bool isDebug = true; // 디버그 로깅 토글

    public bool IsRightGripHeld { get; private set; }
    public bool IsLeftGripHeld { get; private set; }
    public bool IsRightTriggerHeld { get; private set; }
    public bool IsLeftTriggerHeld { get; private set; }


    // 컨트롤러 설정
    private InputAction AButton;        // A 버튼(오른쪽 컨트롤러)에 대한 액션 참조
    private InputAction BButton;        // B 버튼(오른쪽 컨트롤러)에 대한 액션 참조
    private InputAction XButton;        // X 버튼(왼쪽 컨트롤러)에 대한 액션 참조
    private InputAction YButton;        // Y 버튼(왼쪽 컨트롤러)에 대한 액션 참조
    private InputAction RGripButton;    // 오른쪽 그립 버튼(오른쪽 컨트롤러)에 대한 액션 참조
    private InputAction LGripButton;    // 왼쪽 그립 버튼 왼쪽 컨트롤러)에 대한 액션 참조
    private InputAction RTriggerButton; // 오른쪽 트리거 립 버튼(오른쪽 컨트롤러)에 대한 액션 참조
    private InputAction LTriggerButton; // 왼쪽 트리거 버튼(왼쪽 컨트롤러)에 대한 액션 참조

    // 외부 스크립트 참조 (필요에 따라 추가)


    void Start()
    {
        // 입력 액션 바인딩을 설정
        SetupInputActions();
    }

    private void SetupInputActions()
    {
        if (inputActions == null)
        {
            if (isDebug) Debug.LogError("InputActionAsset not found!");
            return;
        }

        // --- 오른쪽 컨트롤러 (A, B 버튼) ---
        var RightActionMap = inputActions?.FindActionMap("XRI Right");
        if (RightActionMap != null)
        {
            AButton = RightActionMap.FindAction("AButton");
            if (AButton != null)
            {
                AButton.Enable();
                AButton.performed += OnAButtonPressed; // 1회성 누르기 이벤트
            }
            else
            {
                if (isDebug) Debug.LogError("AButton action not found!");
            }

            BButton = RightActionMap.FindAction("BButton");
            if (BButton != null)
            {
                BButton.Enable();
                BButton.performed += OnBButtonPressed; // 1회성 누르기 이벤트
            }
            else
            {
                if (isDebug) Debug.LogError("BButton action not found!");
            }
        }
        else
        {
            if (isDebug) Debug.LogError("XRI Right action map not found!");
        }

        // --- 왼쪽 컨트롤러 (X, Y 버튼) ---
        var LeftActionMap = inputActions?.FindActionMap("XRI Left");
        if (LeftActionMap != null)
        {
            XButton = LeftActionMap.FindAction("XButton");
            if (XButton != null)
            {
                XButton.Enable();
                XButton.performed += OnXButtonPressed; // 1회성 누르기 이벤트
            }
            else
            {
                if (isDebug) Debug.LogError("XButton action not found!");
            }

            YButton = LeftActionMap.FindAction("YButton");
            if (YButton != null)
            {
                YButton.Enable();
                YButton.performed += OnYButtonPressed; // 1회성 누르기 이벤트
            }
            else
            {
                if (isDebug) Debug.LogError("YButton action not found!");
            }
        }
        else
        {
            if (isDebug) Debug.LogError("XRI Left action map not found!");
        }

        // --- 오른쪽 컨트롤러 상호작용 (그립, 트리거) ---
        var RightInteractionMap = inputActions?.FindActionMap("XRI Right Interaction");
        if (RightInteractionMap != null)
        {
            RGripButton = RightInteractionMap.FindAction("Select");
            if (RGripButton != null)
            {
                RGripButton.Enable();
                RGripButton.performed += OnRGripPerformed; // 눌렀을 때
                RGripButton.canceled += OnRGripCanceled;   // 뗐을 때
            }
            else
            {
                if (isDebug) Debug.LogError("RGripButton action not found!");
            }

            RTriggerButton = RightInteractionMap.FindAction("Activate");
            if (RTriggerButton != null)
            {
                RTriggerButton.Enable();
                RTriggerButton.performed += OnRTriggerPerformed; // 눌렀을 때
                RTriggerButton.canceled += OnRTriggerCanceled;   // 뗐을 때
            }
            else
            {
                if (isDebug) Debug.LogError("RTriggerButton action not found!");
            }
        }
        else
        {
            if (isDebug) Debug.LogError("XRI Right Interaction action map not found!");
        }

        // --- 왼쪽 컨트롤러 상호작용 (그립, 트리거)
        var LeftInteractionMap = inputActions?.FindActionMap("XRI Left Interaction");
        if (LeftInteractionMap != null)
        {
            LGripButton = LeftInteractionMap.FindAction("Select");
            if (LGripButton != null)
            {
                LGripButton.Enable();
                LGripButton.performed += OnLGripPerformed; // 눌렀을 때
                LGripButton.canceled += OnLGripCanceled;   // 뗐을 때
            }
            else
            {
                if (isDebug) Debug.LogError("LGripButton action not found!");
            }

            LTriggerButton = LeftInteractionMap.FindAction("Activate");
            if (LTriggerButton != null)
            {
                LTriggerButton.Enable();
                LTriggerButton.performed += OnLTriggerPerformed; // 눌렀을 때
                LTriggerButton.canceled += OnLTriggerCanceled;   // 뗐을 때
            }
            else
            {
                if (isDebug) Debug.LogError("LTriggerButton action not found!");
            }
        }
        else
        {
            if (isDebug) Debug.LogError("XRI Left Interaction action map not found!");
        }
    }

    // --- 1회성 버튼 누르기 이벤트 핸들러 ---

    private void OnAButtonPressed(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("A Button Pressed");
    }
    private void OnBButtonPressed(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("B Button Pressed");
    }
    private void OnXButtonPressed(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("X Button Pressed");
    }
    private void OnYButtonPressed(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("Y Button Pressed");
    }


    // 오른쪽 그립
    private void OnRGripPerformed(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("Right Grip Held");
        IsRightGripHeld = true;
    }
    private void OnRGripCanceled(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("Right Grip Released");
        IsRightGripHeld = false;
    }

    // 왼쪽 그립
    private void OnLGripPerformed(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("Left Grip Held");
        IsLeftGripHeld = true;
    }
    private void OnLGripCanceled(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("Left Grip Released");
        IsLeftGripHeld = false;
    }

    // 오른쪽 트리거
    private void OnRTriggerPerformed(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("Right Trigger Held");
        IsRightTriggerHeld = true;
    }
    private void OnRTriggerCanceled(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("Right Trigger Released");
        IsRightTriggerHeld = false;
    }

    // 왼쪽 트리거
    private void OnLTriggerPerformed(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("Left Trigger Held");
        IsLeftTriggerHeld = true;
    }
    private void OnLTriggerCanceled(InputAction.CallbackContext context)
    {
        if (isDebug) Debug.Log("Left Trigger Released");
        IsLeftTriggerHeld = false;
    }


    // 메모리 누수를 방지하기 위해 입력 액션 이벤트 구독을 해지
    private void OnDestroy()
    {
        // 1회성 버튼들
        if (AButton != null) AButton.performed -= OnAButtonPressed;
        if (BButton != null) BButton.performed -= OnBButtonPressed;
        if (XButton != null) XButton.performed -= OnXButtonPressed;
        if (YButton != null) YButton.performed -= OnYButtonPressed;

        // 그립 버튼
        if (RGripButton != null)
        {
            RGripButton.performed -= OnRGripPerformed;
            RGripButton.canceled -= OnRGripCanceled;
        }
        if (LGripButton != null)
        {
            LGripButton.performed -= OnLGripPerformed;
            LGripButton.canceled -= OnLGripCanceled;
        }

        // 트리거 버튼
        if (RTriggerButton != null)
        {
            RTriggerButton.performed -= OnRTriggerPerformed;
            RTriggerButton.canceled -= OnRTriggerCanceled;
        }
        if (LTriggerButton != null)
        {
            LTriggerButton.performed -= OnLTriggerPerformed;
            LTriggerButton.canceled -= OnLTriggerCanceled;
        }
    }
}