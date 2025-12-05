using UnityEngine;
using UnityEditor;

// 사용자가 메뉴를 직접 클릭했을 때 경로를 복사하는 유용한 클래스입니다.
public static class CopyPathMenuItem
{
    // 유니티 에디터 상단 메뉴 [GameObject] -> [Copy Path] 항목을 추가합니다.
    // 계층 창(Hierarchy)에서 우클릭 메뉴로도 나타납니다.
    [MenuItem("GameObject/Copy Path")]
    private static void CopyPath()
    {
        // 현재 선택된 게임 오브젝트를 가져옵니다.
        var obj = Selection.activeGameObject;

        // 선택된 것이 없으면 아무것도 하지 않고 종료합니다.
        if (obj == null)
        {
            return;
        }

        // 초기 경로에 자기 자신의 이름을 넣습니다.
        var path = obj.name;

        // 부모 오브젝트가 없을 때까지(최상위 루트까지) 반복하며 거슬러 올라갑니다.
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;

            // "부모이름/자식이름" 형태로 문자열을 합칩니다.
            // (참고: 원본 코드의 로직을 그대로 두었으나, 문자열 처리 방식이 다소 특이합니다)
            path = string.Format("/{0}/{1}", obj.name, path);
            path = path.Substring(1, path.Length - 1); // 맨 앞의 '/'를 제거
        }

        // 완성된 경로 문자열을 시스템 클립보드(복사/붙여넣기 버퍼)에 저장합니다.
        EditorGUIUtility.systemCopyBuffer = path;

        // (선택 사항) 복사가 되었다고 콘솔에 로그를 남겨주면 더 좋습니다.
        Debug.Log($"Path copied to clipboard: {path}");
    }

    // 위 메뉴가 활성화될 조건을 검사하는 함수입니다. (두 번째 인자가 true임)
    [MenuItem("GameObject/Copy Path", true)]
    private static bool CopyPathValidation()
    {
        // 오직 1개의 오브젝트만 선택되었을 때만 메뉴가 활성화되도록 합니다.
        // 여러 개를 선택하면 메뉴가 회색으로 비활성화됩니다.
        return Selection.gameObjects.Length == 1;
    }
}

/* * [주의] 아래 클래스는 클릭할 때마다 클립보드를 덮어쓰는 문제가 있어 전체 주석 처리(비활성화) 했습니다.
 * 꼭 필요한 경우에만 주석 기호(slash-star)를 풀고 사용하세요.
 */

/*
[InitializeOnLoadAttribute] // 에디터가 로드될 때 자동으로 실행
public static class CopyObjectPath
{
    static CopyObjectPath()
    {
        // 하이어라키 창이 갱신될 때마다 OnHierarchy 함수를 호출하도록 등록
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchy;
    }

    private static GameObject select = null;

    private static void OnHierarchy(int instanceid, Rect selectionrect)
    {
        var go = Selection.activeGameObject;
         
        // 선택된 오브젝트가 없으면 종료
        if (go == null)
            return;

        // 이전에 선택한 오브젝트와 같다면(중복 실행 방지) 종료
        if (select == go)
            return;

        // 현재 선택된 오브젝트를 변수에 저장
        select = go;
        var path = go.name;
 
        // 부모를 타고 올라가며 경로 생성
        while (go.transform.parent != null)
        {
            go = go.transform.parent.gameObject;

            // 부모 이름에 "Canvas"가 포함되어 있다면 경로에서 건너뜀 (UI 작업용 필터로 추정)
            if (go.name.Contains("Canvas"))
                continue;
             
            path = string.Format("/{0}/{1}", go.name, path);
            path = path.Substring(1, path.Length - 1);
        }

        // 경로를 클립보드에 강제 복사 (사용자가 복사해둔 다른 텍스트가 사라질 수 있음)
        EditorGUIUtility.systemCopyBuffer = path;
    }
}
*/