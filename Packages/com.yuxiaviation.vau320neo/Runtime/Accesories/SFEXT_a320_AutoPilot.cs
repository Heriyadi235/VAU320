
using System;
using System.Diagnostics.Eventing.Reader;
using A320VAU.FCU;
using A320VAU.SFEXT;
using SaccFlightAndVehicles;
//using Serilog.Filters;
using TMPro;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using YuxiFlightInstruments.BasicFlightData;

namespace A320VAU.SFEXT {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SFEXT_a320_AutoPilot : UdonSharpBehaviour {
        public APStatus currentAP = APStatus.Off;
        // AP部分
        public A320VAU.FCU.FCU fcu = null;
        [Header("Current Flight Data")]
        public float currentSpeed = 200f;
        public float currentHeading = 360f;
        public float currentAltitude = 5000f;
        public float currentVS = 0f;

        [Header("ILS Localizer & GlideSlope Signals (Simulated)")]
        public bool hasValidILS = false;
        public float locDeviation = 0f; // -1 to 1 (0为对准跑道)
        public float gsDeviation = 0f;  // -1 to 1 (0为在下滑道上)

        [Header("Calculated Flight Control Outputs")]
        [Range(-1f, 1f)] public float pitchOutput;
        [Range(-1f, 1f)] public float rollOutput;
        [Range(0f, 1f)] public float thrustOutput;


        void Start() {
        }

        private void Update() {
            // 实时刷新UI显示
            if (fcu.isAP1Active || fcu.isAP2Active) {
                ProcessLateralControl();
                ProcessVerticalControl();
                ProcessILSApproachLogic();
            }
            //if (isATHRActive) {
            //ProcessAutothrust();
            //}
        }

        private void ProcessLateralControl() {
            float targetHdg = fcu.targetHeading;

            // 横向模式选择
            if (fcu.lateralMode == FCU.LateralFlightMode.LOC) {
                // 跟踪航向道
                rollOutput = Mathf.Clamp(locDeviation * 2.0f, -1f, 1f);
                return;
            }

            if (fcu.lateralGuidance == FCU.GuidanceMode.Managed) {
                // 模拟从 FMGC 读出的航线航向
                targetHdg = GetFMGCHeading();
            }

            // 简单P控制器算 Roll 角度
            float hdgError = Mathf.DeltaAngle(currentHeading, targetHdg);
            rollOutput = Mathf.Clamp(hdgError * 0.05f, -1f, 1f);
        }

        private void ProcessVerticalControl() {
            float targetAlt = fcu.targetAltitude;

            // 下滑道跟踪模式
            if (fcu.verticalMode == FCU.VerticalFlightMode.GS) {
                pitchOutput = Mathf.Clamp(-gsDeviation * 1.5f, -1f, 1f);
                return;
            }

            // VS / OP CLB / CLB / ALT HOLD 逻辑分发
            if (fcu.verticalMode == FCU.VerticalFlightMode.VS) {
                float vsError = fcu.targetVS - currentVS;
                pitchOutput = Mathf.Clamp(vsError * 0.001f, -1f, 1f);
            }
            else {
                float altError = targetAlt - currentAltitude;
                pitchOutput = Mathf.Clamp(altError * 0.0005f, -0.8f, 0.8f);
            }
        }

        private void ProcessILSApproachLogic() {
            if (!hasValidILS || fcu == null) return;

            // 截获 LOC (航向道)
            if (fcu.locStatus == FCU.ApprModeStatus.Armed || fcu.apprStatus == FCU.ApprModeStatus.Armed) {
                if (Mathf.Abs(locDeviation) < 0.3f) {
                    fcu.locStatus = FCU.ApprModeStatus.Engaged;
                    fcu.lateralMode = FCU.LateralFlightMode.LOC;
                    fcu.lateralGuidance = FCU.GuidanceMode.Selected;
                }
            }

            // 截获 GS (下滑道)
            if (fcu.apprStatus == FCU.ApprModeStatus.Armed && fcu.locStatus == FCU.ApprModeStatus.Engaged) {
                if (Mathf.Abs(gsDeviation) < 0.2f) {
                    fcu.apprStatus = FCU.ApprModeStatus.Engaged;
                    fcu.verticalMode = FCU.VerticalFlightMode.GS;
                    fcu.verticalGuidance = FCU.GuidanceMode.Selected;
                }
            }
        }

        private void ProcessAutothrust() {
            float targetSpd = fcu.targetSpeed;
            float spdError = targetSpd - currentSpeed;
            thrustOutput = Mathf.Clamp01(0.5f + spdError * 0.05f);
        }

        private float GetFMGCHeading() { return 270f; } // 模拟FMGC航线数据

    }
}


