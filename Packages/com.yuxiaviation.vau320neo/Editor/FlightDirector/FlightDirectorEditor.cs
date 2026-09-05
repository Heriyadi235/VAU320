#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace A320VAU.Avionics {
    [CustomEditor(typeof(FlightDirector))]
    public class FlightDirectorEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            // 绘制默认面板属性
            DrawDefaultInspector();

            FlightDirector fd = (FlightDirector)target;
            // 绘制默认属性
            serializedObject.Update();

            /*
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("--- 引用 ---", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fcu"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fmgc"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxPitchDev"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxRollDev"));
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("--- 状态 ---", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("vMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isFDOn"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("currentRWYHeading"));
            
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("--- 控制器 ---", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("altHoldKp"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("altHoldKi"));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("altStarKp"));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("altDiff"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("altDiff"));
            
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("opKp"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("speedDiff"));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headingKp"));

            */
            EditorGUILayout.LabelField("--- 动态运行监视 (Live Readout) ---", EditorStyles.boldLabel);

            // 在 Editor 下以进度条方式直观监视归一化动画输出 [0, 1]
            if (Application.isPlaying) {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), fd.fdVerNormalized, $"FD_ver (俯仰): {fd.fdVerNormalized:F3}");
                EditorGUILayout.Space(2);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), fd.fdHorNormalized, $"FD_hor (滚转): {fd.fdHorNormalized:F3}");

                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    $"[俯仰通道] 目标: {fd.debugTargetPitch:F2}° | 当前: {fd.debugCurrentPitch:F2}° | 偏差: {(fd.debugTargetPitch - fd.debugCurrentPitch):F2}°\n" +
                    $"[滚转通道] 目标: {fd.debugTargetRoll:F2}° | 当前: {fd.debugCurrentRoll:F2}° | 偏差: {(fd.debugTargetRoll - fd.debugCurrentRoll):F2}°",
                    MessageType.Info
                );
            }
            else {
                EditorGUILayout.HelpBox("进入 Play 模式后查看动态偏差与动画输出进度条。", MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif