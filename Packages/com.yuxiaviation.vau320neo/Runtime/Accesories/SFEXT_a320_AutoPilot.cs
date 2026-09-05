using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using A320VAU.FCU;
using A320VAU.Avionics;
using SaccFlightAndVehicles;
using A320VAU.ADIRU;
using System.Reflection;
using UnityEditor;
using A320VAU.Autopilot;
using System;

namespace A320VAU.Autopilot {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class SFEXT_a320_AutoPilot : UdonSharpBehaviour {
        [Header("--- 系统引用 ---")]
        public UdonSharpBehaviour SAVControl;   // SaccFlight 主控制器
        public FlightDirector flightDirector;    // 飞行指引仪
        public FCU.FCU fcu;                     // FCU 控制面板
        public ADIRU.ADIRU adiru;
        [Header("--- 俯仰轴 (Pitch) PID 参数 ---")]
        public float pitchP = 0.08f;
        public float pitchI = 0.02f;
        public float pitchD = 0.015f;
        private float _pitchIntegrator;
        private float _lastPitchError;

        [Header("--- 滚转轴 (Roll) PID 参数 ---")]
        public float rollP = 0.05f;
        public float rollD = 0.01f;
        private float _lastRollError;

        [Header("--- 偏航轴 (Yaw) 阻尼参数 ---")]
        public float yawDamp = 10f;

        [Header("--- A320 包线保护限制 (Protections) ---")]
        public float gLimitMax = 2.5f;     // A320 正 G 极限 (卡民航 2.5G)
        public float gLimitMin = -1.0f;    // A320 负 G 极限 (-1.0G)
        public float aoaLimiter = 12f;      // 迎角保护阈值 (度)

        // 内部状态标志
        private SaccEntity _entityControl;
        private Rigidbody _vehicleRigidbody;
        private Transform _vehicleTransform;

        private Vector3 _rotationInputs = Vector3.zero;
        private bool _isAPActive = false;
        private bool _joystickOverridden = false;
        private bool _isOwner = false;

        private void Start() {
            if (SAVControl != null) {
                _entityControl = (SaccEntity)SAVControl.GetProgramVariable("EntityControl");
                _vehicleRigidbody = (Rigidbody)SAVControl.GetProgramVariable("VehicleRigidbody");
                _vehicleTransform = _entityControl.transform;
                _isOwner = (bool)SAVControl.GetProgramVariable("IsOwner");
            }
        }

        private void FixedUpdate() {
            // 同步 AP 激活状态 (AP1 或 AP2 任意开启即激活)
            bool shouldAPBeActive = fcu != null && (fcu.isAP1Active || fcu.isAP2Active);

            if (shouldAPBeActive != _isAPActive) {
                if (shouldAPBeActive) ActivateAutopilot();
                else DeactivateAutopilot();
            }

            // 仅在 AP 激活且本地为机主 (Owner) 时计算操控输入
            if (!_isAPActive || !_isOwner) return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0) return;

            // 当前俯仰角 (Pitch: 抬头为正，俯头为负)
            float currentPitch = adiru.irs.pitch;

            // 当前滚转角 (Roll: 右倾为正，左倾为负)
            float currentRoll = adiru.irs.bank;


            // 2. 从 FlightDirector 获取目标姿态
            float targetPitch = flightDirector != null ? flightDirector.targetPitch : 0f;
            float targetRoll = flightDirector != null ? flightDirector.targetRoll : 0f;

            // ----------------------------------------------------
            // 3. 俯仰轴 (Pitch) PID 计算
            // ----------------------------------------------------
            float pitchError = -(targetPitch - currentPitch);
            _pitchIntegrator += pitchError * deltaTime;
            _pitchIntegrator = Mathf.Clamp(_pitchIntegrator, -5f, 5f); // 积分防饱和

            float pitchDerivator = (pitchError - _lastPitchError) / deltaTime;
            _lastPitchError = pitchError;

            float pitchCmd = (pitchError * pitchP) + (_pitchIntegrator * pitchI) + (pitchDerivator * pitchD);

            // ----------------------------------------------------
            // 4. 滚转轴 (Roll) PD 计算
            // ----------------------------------------------------
            float rollError = -(targetRoll - currentRoll);
            float rollDerivator = (rollError - _lastRollError) / deltaTime;
            _lastRollError = rollError;

            float rollCmd = (rollError * rollP) + (rollDerivator * rollD);

            // ----------------------------------------------------
            // 5. 偏航轴 (Yaw) 阻尼计算 (抑制阻尼滚转/荷兰滚)
            // ----------------------------------------------------
            float yawCmd = Mathf.Clamp(adiru.irs.trackSlipAngle * yawDamp, -1f, 1f);

            // ----------------------------------------------------
            // 6. A320 G力与迎角保护 (Alpha / G Limiter)
            // ----------------------------------------------------
            float currentG = (float)SAVControl.GetProgramVariable("VertGs");
            float currentAoA = Mathf.Abs((float)SAVControl.GetProgramVariable("AngleOfAttack"));

            // G力限制衰减
            float gLimitFactor = 1.0f;
            if (currentG > gLimitMax) gLimitFactor = Mathf.Clamp01(1.0f - (currentG - gLimitMax));
            else if (currentG < gLimitMin) gLimitFactor = Mathf.Clamp01(1.0f - (gLimitMin - currentG));

            // 迎角限制衰减
            float aoaLimitFactor = Mathf.Clamp01(1.0f - (currentAoA / aoaLimiter));

            // 综合保护限制
            float protectionFactor = Mathf.Min(gLimitFactor, aoaLimitFactor);
            
            //速度限制
            protectionFactor *= Mathf.Lerp(0.8f, 0.2f, (adiru.adr.trueAirSpeed - 250) / (350-250));
            pitchCmd *= protectionFactor;

            // 7. 组装输入指令并注入 SAVControl
            
                
            _rotationInputs.x = Mathf.Clamp(pitchCmd, -1f, 1f);
            _rotationInputs.z = Mathf.Clamp(rollCmd, -1f, 1f);
            _rotationInputs.y = yawCmd;

            SAVControl.SetProgramVariable("JoystickOverride", _rotationInputs);
        }

        // 辅助函数：将滚转角收拢至 -180 ~ 180 度
        private bool errorRollClamp(ref float roll) {
            if (roll > 180f) roll -= 360f;
            return true;
        }

        // 激活 AP 控制权接管
        public void ActivateAutopilot() {
            if (_isAPActive) return;
            _isAPActive = true;

            _pitchIntegrator = 0f;
            _lastPitchError = 0f;
            _lastRollError = 0f;

            if (!_joystickOverridden && SAVControl != null) {
                int currentOverrideCount = (int)SAVControl.GetProgramVariable("JoystickOverridden");
                SAVControl.SetProgramVariable("JoystickOverridden", currentOverrideCount + 1);
                _joystickOverridden = true;
            }
        }

        // 释放 AP 控制权
        public void DeactivateAutopilot() {
            if (!_isAPActive) return;
            _isAPActive = false;

            if (_joystickOverridden && SAVControl != null) {
                int currentOverrideCount = (int)SAVControl.GetProgramVariable("JoystickOverridden");
                SAVControl.SetProgramVariable("JoystickOverridden", Mathf.Max(0, currentOverrideCount - 1));
                _joystickOverridden = false;
            }

            if (SAVControl != null) {
                SAVControl.SetProgramVariable("JoystickOverride", Vector3.zero);
            }
            _rotationInputs = Vector3.zero;
        }

        // SaccFlight 系统的机主接管与流失事件处理
        public void SFEXT_O_TakeOwnership() => _isOwner = true;
        public void SFEXT_O_LoseOwnership() {
            _isOwner = false;
            DeactivateAutopilot();
        }

        // 摇杆手动抓取时自动断开 AP (人工抢操脱扣逻辑)
        public void SFEXT_O_JoystickGrabbed() {
            if (_isAPActive) {
                if (fcu != null) {
                    fcu.isAP1Active = false;
                    fcu.isAP2Active = false;
                }
                DeactivateAutopilot();
            }
        }
    }

}

#if UNITY_EDITOR
    [CustomEditor(typeof(A320VAU.Autopilot.SFEXT_a320_AutoPilot))]
    public class A320_AutopilotEditor : Editor {
        // 缓存 Reflection 字段以提升 Editor 渲染性能
        private FieldInfo _isAPActiveField;
        private FieldInfo _isOwnerField;
        private FieldInfo _joystickOverriddenField;
        private FieldInfo _rotationInputsField;
        private FieldInfo _pitchIntegratorField;

        private void OnEnable() {
            var type = typeof(SFEXT_a320_AutoPilot);
            _isAPActiveField = type.GetField("_isAPActive", BindingFlags.NonPublic | BindingFlags.Instance);
            _isOwnerField = type.GetField("_isOwner", BindingFlags.NonPublic | BindingFlags.Instance);
            _joystickOverriddenField = type.GetField("_joystickOverridden", BindingFlags.NonPublic | BindingFlags.Instance);
            _rotationInputsField = type.GetField("_rotationInputs", BindingFlags.NonPublic | BindingFlags.Instance);
            _pitchIntegratorField = type.GetField("_pitchIntegrator", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SFEXT_a320_AutoPilot ap = (SFEXT_a320_AutoPilot)target;

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("【A320 自动驾驶 PID 调优与响应监控台】", MessageType.Info);

            // ----------------------------------------------------
            // 1. 系统引用与运行状态指示
            // ----------------------------------------------------
            EditorGUILayout.LabelField("1. 系统引用与接管状态", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SAVControl"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("flightDirector"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fcu"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("adiru"));

        if (Application.isPlaying) {
                bool isAPActive = _isAPActiveField != null && (bool)_isAPActiveField.GetValue(ap);
                bool isOwner = _isOwnerField != null && (bool)_isOwnerField.GetValue(ap);
                bool isOverridden = _joystickOverriddenField != null && (bool)_joystickOverriddenField.GetValue(ap);

                EditorGUILayout.BeginVertical("box");
                GUI.backgroundColor = isAPActive ? Color.green : Color.grey;
                EditorGUILayout.LabelField("AP 激活状态:", isAPActive ? "ENGAGED (已接管)" : "OFF (未激活)", EditorStyles.boldLabel);
                GUI.backgroundColor = Color.white;

                EditorGUILayout.LabelField("本地机主 (Owner):", isOwner ? "Yes" : "No (不计算物理)");
                EditorGUILayout.LabelField("SAVControl 覆盖计数:", isOverridden ? "Active (+1)" : "Inactive (0)");
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // ----------------------------------------------------
            // 2. PID 增益实时调整参数
            // ----------------------------------------------------
            EditorGUILayout.LabelField("2. PID 增益参数 (可实时调整)", EditorStyles.boldLabel);

            // 俯仰轴
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("俯仰轴 (Pitch Axis)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pitchP"), new GUIContent("P (比例增益)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pitchI"), new GUIContent("I (积分增益)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pitchD"), new GUIContent("D (微分增益)"));
            EditorGUILayout.EndVertical();

            // 滚转与偏航轴
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("滚转与偏航轴 (Roll & Yaw Axis)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rollP"), new GUIContent("Roll P (滚转比例)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rollD"), new GUIContent("Roll D (滚转微分)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("yawDamp"), new GUIContent("Yaw Damp (偏航阻尼)"));
            EditorGUILayout.EndVertical();

            // A320 包线保护阈值
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("A320 包线保护限制 (Protection Limits)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gLimitMax"), new GUIContent("最大正 G 限制"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gLimitMin"), new GUIContent("最小负 G 限制"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("aoaLimiter"), new GUIContent("迎角保护阈值 (°)"));
            EditorGUILayout.EndVertical();

            // ----------------------------------------------------
            // 3. 运行期输入/输出动态响应监测 (Play Mode Only)
            // ----------------------------------------------------
            if (Application.isPlaying) {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("3. 动态响应与控制量输出监测", EditorStyles.boldLabel);

                Vector3 rotationInputs = _rotationInputsField != null ? (Vector3)_rotationInputsField.GetValue(ap) : Vector3.zero;
                float pitchIntegrator = _pitchIntegratorField != null ? (float)_pitchIntegratorField.GetValue(ap) : 0f;

                float targetPitch = ap.flightDirector != null ? ap.flightDirector.targetPitch : 0f;
                float targetRoll = ap.flightDirector != null ? ap.flightDirector.targetRoll : 0f;

                EditorGUILayout.BeginVertical("box");

                // 俯仰监测
                EditorGUILayout.LabelField($"[Pitch] FD目标: {targetPitch:F2}° | 积分累积: {pitchIntegrator:F3}");
                DrawBiDirectionalProgressBar(rotationInputs.x, "Pitch Axis Cmd (X)");

                EditorGUILayout.Space(5);

                // 滚转监测
                EditorGUILayout.LabelField($"[Roll] FD目标: {targetRoll:F2}°");
                DrawBiDirectionalProgressBar(rotationInputs.z, "Roll Axis Cmd (Z)");

                EditorGUILayout.Space(5);

                // 偏航阻尼监测
                EditorGUILayout.LabelField("[Yaw] 阻尼输出");
                DrawBiDirectionalProgressBar(rotationInputs.y, "Yaw Axis Cmd (Y)");

                EditorGUILayout.EndVertical();

                // 手动强切测试按钮
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("测试：强行挂载 AP")) ap.ActivateAutopilot();
                if (GUILayout.Button("测试：强行断开 AP")) ap.DeactivateAutopilot();
                EditorGUILayout.EndHorizontal();

                // Play 模式下持续重绘 Inspector
                Repaint();
            }

            serializedObject.ApplyModifiedProperties();
        }

        // 绘制双向指示条 (范围 -1.0 到 +1.0)
        private void DrawBiDirectionalProgressBar(float value, string label) {
            float normalized = Mathf.Clamp01((value + 1.0f) / 2.0f);
            Rect rect = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(rect, normalized, $"{label}: {value:F3}");
        }
    }
#endif
