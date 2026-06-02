using Game.Core.UI;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Inspector.UI
{
    /// <summary>
    /// SliderValue のカスタムエディタ。
    /// _value を Slider の m_Value と同様に min〜max のつまみ（スライダー）で編集できるようにする。
    /// step に snap するため、Inspector 表示値と Runtime 適用値が一致する。
    /// </summary>
    [CustomEditor(typeof(SliderValue))]
    public class SliderValueEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var scriptProp = serializedObject.FindProperty("m_Script");
            var sliderProp = serializedObject.FindProperty("_slider");
            var minProp = serializedObject.FindProperty("_min");
            var maxProp = serializedObject.FindProperty("_max");
            var stepProp = serializedObject.FindProperty("_step");
            var valueProp = serializedObject.FindProperty("_value");
            var valueTextProp = serializedObject.FindProperty("_valueText");
            var valueTextFormatProp = serializedObject.FindProperty("_valueTextFormat");

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(scriptProp);

            EditorGUILayout.PropertyField(sliderProp);
            EditorGUILayout.PropertyField(minProp);
            EditorGUILayout.PropertyField(maxProp);
            EditorGUILayout.PropertyField(stepProp);

            // _value を min〜max のつまみで編集（step に snap）
            var min = minProp.floatValue;
            var max = maxProp.floatValue;
            var step = stepProp.floatValue;

            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUILayout.Slider(valueProp.displayName, valueProp.floatValue, min, max);
            if (EditorGUI.EndChangeCheck())
            {
                if (step > 0f)
                    newValue = min + Mathf.Round((newValue - min) / step) * step;
                valueProp.floatValue = Mathf.Clamp(newValue, min, max);
            }

            EditorGUILayout.PropertyField(valueTextProp);
            EditorGUILayout.PropertyField(valueTextFormatProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
