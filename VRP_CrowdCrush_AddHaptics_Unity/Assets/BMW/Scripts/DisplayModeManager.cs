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

    [Header("Display Mode Settings (Inspector)")]
    [Tooltip("표시할 디스플레이 인덱스 (0: Display 1, 1: Display 2 ...)")]
    [SerializeField] private int targetDisplayIndex = 1;

    [Tooltip("전체화면 팝업 창의 해상도 (너비 x 높이)")]
    [SerializeField] private Vector2 displayResolution = new Vector2(1920, 1080);

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

    void ActivateMultiDisplay()
    {
        if (Display.displays.Length > targetDisplayIndex && targetDisplayIndex > 0)
        {
            Display.displays[targetDisplayIndex].Activate();
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
            if (currentDisplayMode == DisplayMode.Display)
            {
                Debug.Log("[Manager] ESC 눌림 -> 전체화면 해제 및 기본창 복구");
                currentDisplayMode = DisplayMode.OnlyVR;
            }
        }
    }

    void ApplyScreenMode()
    {
        switch (currentDisplayMode)
        {
            case DisplayMode.OnlyVR:
                FullScreenOff();
                break;

            case DisplayMode.Cave:
                SetEditorPopupState(false);
                if (!Screen.fullScreen)
                {
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    Screen.fullScreen = true;
                }
                break;

            case DisplayMode.Display:
                SetEditorPopupState(true);
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
        SetEditorPopupState(false);
        if (Screen.fullScreen)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }
    }

    private void SetEditorPopupState(bool isOpen)
    {
#if UNITY_EDITOR
        if (isOpen)
            FullscreenGameView.Open(targetDisplayIndex, displayResolution);
        else
            FullscreenGameView.Close(true, displayResolution);
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

        // 1. 기존 창 닫기 로직 (엄격한 타입 검사 적용)
        var allGameViews = Resources.FindObjectsOfTypeAll(GameViewType);
        foreach (var view in allGameViews)
        {
            // EditorWindow로 캐스팅이 안되면 무시
            if (!(view is EditorWindow window)) continue;

            // [가장 중요한 수정]
            // 상속받은 자식 클래스(SimulatorWindow 등)는 제외하고
            // 정확히 'UnityEditor.GameView' 타입인 것만 닫습니다.
            if (view.GetType() != GameViewType) continue;

            try
            {
                window.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FullscreenGameView] 창을 닫는 중 오류 발생(무시됨): {e.Message}");
            }
        }

        instance = null;
        instance = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);

        ShowToolbarProperty?.SetValue(instance, False);

        if (TargetDisplayField != null)
        {
            TargetDisplayField.SetValue(instance, targetDisplayIndex);
        }

        SetGameViewSize(instance, (int)size.x, (int)size.y);

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

            if (size.x > 0 && size.y > 0)
            {
                SetGameViewSize(restoredWindow, (int)size.x, (int)size.y);
            }
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

                int w = (int)widthProp.GetValue(gameViewSize, null);
                int h = (int)heightProp.GetValue(gameViewSize, null);

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
        }
        catch (Exception e)
        {
            // 해상도 설정 중 오류가 나도 게임은 멈추지 않도록 로그만 찍음
            Debug.LogWarning($"[FullscreenGameView] 해상도 프리셋 자동 설정 실패: {e.Message}");
        }
    }
}
#endif