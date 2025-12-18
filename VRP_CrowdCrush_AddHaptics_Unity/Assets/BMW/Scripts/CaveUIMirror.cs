using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CaveUIMirror : MonoBehaviour
{
    [Header("거리 설정")]
    [Tooltip("UI를 카메라 앞 몇 미터에 띄울지 (너무 멀면 벽에 가려짐)")]
    public float uiDistance = 2.0f;

    private GameObject mirrorUIObj;

    void Start()
    {
        StartCoroutine(InitRoutine());
    }

    IEnumerator InitRoutine()
    {
        while (DisplayModeManager.Instance == null) yield return null;

        DisplayModeManager.Instance.OnDisplayModeChanged += HandleModeChange;
        HandleModeChange(DisplayModeManager.Instance.CurrentDisplayMode);
    }

    void OnDestroy()
    {
        if (DisplayModeManager.Instance != null)
            DisplayModeManager.Instance.OnDisplayModeChanged -= HandleModeChange;
    }

    void HandleModeChange(DisplayModeManager.DisplayMode mode)
    {
        if (mode == DisplayModeManager.DisplayMode.Cave)
        {
            StartCoroutine(TryCreateMirrorUI());
        }
        else
        {
            StopAllCoroutines();
            DestroyMirrorUI();
        }
    }

    IEnumerator TryCreateMirrorUI()
    {
        if (mirrorUIObj != null) yield break;

        Camera targetCam = null;

        // 카메라 찾을 때까지 무한 재시도 (안전장치)
        while (targetCam == null)
        {
            // 1. 싱글톤에서 찾기
            if (CaveCameraController.Instance != null)
                targetCam = CaveCameraController.Instance.frontCamera;

            // 2. 이름으로 찾기 (비상용)
            if (targetCam == null)
            {
                GameObject obj = GameObject.Find("Camera - Front");
                if (obj != null) targetCam = obj.GetComponent<Camera>();
            }

            if (targetCam == null) yield return new WaitForSeconds(0.2f);
        }

        CreateMirrorUI(targetCam);
    }

    void CreateMirrorUI(Camera cam)
    {
        if (mirrorUIObj != null) return;

        // [해결책 1] 카메라가 UI 레이어를 무조건 보게 설정 (비트 연산)
        int uiLayer = LayerMask.NameToLayer("UI");
        cam.cullingMask |= (1 << uiLayer);

        // 1. UI 복제
        mirrorUIObj = Instantiate(gameObject, transform.parent);
        mirrorUIObj.name = gameObject.name + "_CaveMirror";

        // 2. 미러링 스크립트 제거
        Destroy(mirrorUIObj.GetComponent<CaveUIMirror>());

        // 3. 캔버스 설정
        Canvas mirrorCanvas = mirrorUIObj.GetComponent<Canvas>();
        mirrorCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        mirrorCanvas.worldCamera = cam;
        mirrorCanvas.planeDistance = uiDistance; // 거리 강제 적용
        mirrorCanvas.sortingOrder = 999; // 무조건 제일 앞에 그리기
        mirrorCanvas.targetDisplay = 0; // Display 1

        // 4. 스케일러 강제 조정 (3면 화면이라 비율이 깨질 수 있음)
        CanvasScaler scaler = mirrorUIObj.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); // 기준 해상도 고정
            scaler.matchWidthOrHeight = 0.5f;
        }

        // [해결책 2] 복제된 UI의 레이어를 강제로 'UI'로 변경
        SetLayerRecursively(mirrorUIObj, uiLayer);

        Debug.Log($"[CaveUI] 생성 완료! 대상 카메라: {cam.name}, 거리: {uiDistance}");
    }

    void DestroyMirrorUI()
    {
        if (mirrorUIObj != null)
        {
            Destroy(mirrorUIObj);
            mirrorUIObj = null;
        }
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}