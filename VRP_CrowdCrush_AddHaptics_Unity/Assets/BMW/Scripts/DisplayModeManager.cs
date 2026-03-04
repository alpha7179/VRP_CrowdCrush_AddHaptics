using UnityEngine;
using System;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

public class DisplayModeManager : MonoBehaviour
{
    public static DisplayModeManager Instance { get; private set; }

    public enum DisplayMode { OnlyVR, Display, Cave }

    [Header("State")]
    [SerializeField] private DisplayMode currentDisplayMode;
    private DisplayMode previousDisplayMode;

    [Header("Display Settings")]
    [Tooltip("표시할 타겟 디스플레이 인덱스 (0: Display 1, 1: Display 2 ...)")]
    [SerializeField] private int targetDisplayIndex = 1;

    [Header("Resolution Configuration")]
    [Tooltip("Display 모드일 때 사용할 해상도 (보통 1920x1080)")]
    [SerializeField] private Vector2 displayResolution = new Vector2(1920, 1080);

    [Tooltip("Cave 모드일 때 사용할 해상도 (48:9 비율 -> 예: 5760x1080)")]
    [SerializeField] private Vector2 caveResolution = new Vector2(5760, 1080);

    [Tooltip("디스플레이 주사율 (보통 60)")]
    [SerializeField] private int refreshRate = 60;

    public event Action<DisplayMode> OnDisplayModeChanged;
    public DisplayMode CurrentDisplayMode => currentDisplayMode;

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

        ActivateMultiDisplay();
    }

    /// <summary>
    /// 현재 설정된 모드에 맞는 해상도로 디스플레이를 활성화합니다.
    /// </summary>
    void ActivateMultiDisplay()
    {
        Debug.Log($"[DisplayManager] 감지된 모니터 개수: {Display.displays.Length}");

        if (targetDisplayIndex > 0 && Display.displays.Length > targetDisplayIndex)
        {
            // 현재 모드에 따라 해상도 결정
            int w, h;
            if (currentDisplayMode == DisplayMode.Cave)
            {
                w = (int)caveResolution.x;
                h = (int)caveResolution.y;
            }
            else // Display 모드 혹은 기본
            {
                w = (int)displayResolution.x;
                h = (int)displayResolution.y;
            }

            // 해당 해상도로 활성화
            if (!Display.displays[targetDisplayIndex].active)
            {
                // 주사율을 새로운 구조체 형식으로 변환하여 전달
                Display.displays[targetDisplayIndex].Activate(w, h, new RefreshRate() { numerator = (uint)refreshRate, denominator = 1 });
                Debug.Log($"[DisplayManager] Display {targetDisplayIndex + 1} 활성화됨 ({w}x{h})");
            }
        }
        else if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
    }

    IEnumerator Start()
    {
        yield return null;
        ApplyScreenMode();
        previousDisplayMode = currentDisplayMode;
    }

    void Update()
    {
        if (currentDisplayMode != previousDisplayMode)
        {
            ApplyScreenMode();
            previousDisplayMode = currentDisplayMode;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Display나 Cave 모드에서 ESC를 누르면 VR 모드(기본)로 복귀
            if (currentDisplayMode == DisplayMode.Display || currentDisplayMode == DisplayMode.Cave)
            {
                Debug.Log("[Manager] ESC 눌림 -> OnlyVR 모드로 전환");
                currentDisplayMode = DisplayMode.OnlyVR;
            }
        }
    }

    void ApplyScreenMode()
    {
        switch (currentDisplayMode)
        {
            case DisplayMode.OnlyVR:
                SetEditorPopupState(false, Vector2.zero); // 팝업 닫기

                // 빌드: 창모드
                if (Screen.fullScreen)
                {
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    Screen.fullScreen = false;
                }
                break;

            case DisplayMode.Display:
                // [Display 모드] 1920x1080 적용
                SetEditorPopupState(true, displayResolution);

                // 빌드 설정
                if (!Screen.fullScreen)
                {
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    Screen.fullScreen = true;
                }
                break;

            case DisplayMode.Cave:
                // [Cave 모드] 48:9 (5760x1080) 적용
                // 에디터에서도 확인하고 싶다면 true로 설정, 끄고 싶다면 false
                SetEditorPopupState(true, caveResolution);

                // 빌드 설정 (해상도 재적용 및 전체화면)
                ActivateMultiDisplay(); // 해상도 변경을 위해 재호출

                if (!Screen.fullScreen)
                {
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    Screen.fullScreen = true;
                }
                break;
        }

        OnDisplayModeChanged?.Invoke(currentDisplayMode);
        Debug.Log($"[Manager] 모드 변경됨: {currentDisplayMode}");
    }

    public void FullScreenOff()
    {
        SetEditorPopupState(false, Vector2.zero);
        if (Screen.fullScreen)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }
    }

    private void SetEditorPopupState(bool isOpen, Vector2 size)
    {
#if UNITY_EDITOR
        if (isOpen)
            FullscreenGameView.Open(targetDisplayIndex, size);
        else
            FullscreenGameView.Close(true, size); // 닫을 때도 해상도 정보를 넘겨 복구 가능
#endif
    }
}

#if UNITY_EDITOR
public static class FullscreenGameView
{
    static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
    static readonly FieldInfo TargetDisplayField = GameViewType.GetField("m_TargetDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly PropertyInfo ShowToolbarProperty = GameViewType.GetProperty("showToolbar", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly object False = false;

    static EditorWindow instance;

    static FullscreenGameView()
    {
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
    }

    private static void OnBeforeAssemblyReload()
    {
        Close(false, Vector2.zero);
    }

    public static void Open(int targetDisplayIndex, Vector2 size)
    {
        if (GameViewType == null) return;

        // 기존 창 닫기 (SimulatorWindow 제외)
        var allGameViews = Resources.FindObjectsOfTypeAll(GameViewType);
        foreach (var view in allGameViews)
        {
            if (!(view is EditorWindow window)) continue;
            if (EditorUtility.IsPersistent(window)) continue;
            if (view.GetType() != GameViewType) continue;

            try { window.Close(); } catch { }
        }

        instance = null;
        instance = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);

        ShowToolbarProperty?.SetValue(instance, False);

        if (TargetDisplayField != null)
        {
            TargetDisplayField.SetValue(instance, targetDisplayIndex);
        }

        // 요청된 해상도(Size)로 GameView 프리셋 설정
        SetGameViewSize(instance, (int)size.x, (int)size.y);

        // 팝업 창 크기 및 위치 설정
        var fullscreenRect = new Rect(0, 0, size.x, size.y);
        instance.ShowPopup();
        instance.position = fullscreenRect;
        instance.Focus();
    }

    public static void Close(bool restoreDefaultWindow, Vector2 size)
    {
        if (instance != null)
        {
            instance.Close();
            instance = null;
        }

        if (restoreDefaultWindow)
        {
            var restoredWindow = EditorWindow.GetWindow(GameViewType);
            restoredWindow.Show();

            // 복구 시에는 기본 1920x1080이나 지정된 사이즈로 복구
            if (size.x > 0)
                SetGameViewSize(restoredWindow, (int)size.x, (int)size.y);
        }
    }

    private static void SetGameViewSize(EditorWindow gameViewWindow, int width, int height)
    {
        try
        {
            var gameViewSizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var singleType = typeof(ScriptableSingleton<>).MakeGenericType(gameViewSizesType);
            var instanceProp = singleType.GetProperty("instance");
            var getGroupMethod = gameViewSizesType.GetMethod("GetGroup");

            var gameViewSizesInstance = instanceProp.GetValue(null, null);
            var currentGroupType = (int)GameViewType.GetProperty("currentSizeGroupType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static).GetValue(gameViewWindow, null);
            var group = getGroupMethod.Invoke(gameViewSizesInstance, new object[] { currentGroupType });

            var getTotalCountMethod = group.GetType().GetMethod("GetTotalCount");
            var getGameViewSizeMethod = group.GetType().GetMethod("GetGameViewSize");
            int totalCount = (int)getTotalCountMethod.Invoke(group, null);

            int targetIndex = -1;

            for (int i = 0; i < totalCount; i++)
            {
                var gameViewSize = getGameViewSizeMethod.Invoke(group, new object[] { i });
                var widthProp = gameViewSize.GetType().GetProperty("width");
                var heightProp = gameViewSize.GetType().GetProperty("height");
                // var typeProp = gameViewSize.GetType().GetProperty("sizeType"); // 필요한 경우 타입 체크

                int w = (int)widthProp.GetValue(gameViewSize, null);
                int h = (int)heightProp.GetValue(gameViewSize, null);

                // 정확히 픽셀 크기가 일치하는 프리셋을 찾음
                if (w == width && h == height)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex != -1)
            {
                var selectedSizeIndexProp = GameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                selectedSizeIndexProp.SetValue(gameViewWindow, targetIndex, null);
                gameViewWindow.Repaint();
            }
            else
            {
                Debug.LogWarning($"[FullscreenGameView] 해상도 {width}x{height}에 맞는 GameView 프리셋을 찾을 수 없습니다. Game View 설정에서 'Fixed Resolution'으로 해당 크기를 추가해주세요.");
            }
        }
        catch (Exception) { }
    }
}
#endif