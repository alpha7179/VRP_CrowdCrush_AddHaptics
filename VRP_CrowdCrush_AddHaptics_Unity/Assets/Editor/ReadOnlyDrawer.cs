using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // UI를 비활성화 상태(회색)로 만듭니다
        GUI.enabled = false;

        // 프로퍼티를 그립니다
        EditorGUI.PropertyField(position, property, label, true);

        // 다시 UI를 활성화 상태로 돌려놓습니다
        GUI.enabled = true;
    }
}