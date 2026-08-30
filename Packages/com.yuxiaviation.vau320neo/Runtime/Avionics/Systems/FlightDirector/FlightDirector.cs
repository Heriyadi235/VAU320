using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

using A320VAU.FCU;
using VRC.Core;
namespace A320VAU.Avionics {
    public enum FDVerticalMode { OFF, SRS, OP_CLB, ALT, VS, FPA, OP_DES }
    public enum FDLateralMode { OFF, RWY, RWY_TRK, HDG, NAV, TRK }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FlightDirector : UdonSharpBehaviour {
        [Header("--- References ---")]
        public FCU.FCU fcu;

        [Header("--- Performance Tuning ---")]
        [Tooltip("FD 俯仰杆达到最大偏转所需的偏差角度(度)")]
        public float maxPitchDev = 15.0f;
        [Tooltip("FD 滚转杆达到最大偏转所需的偏差角度(度)")]
        public float maxRollDev = 30.0f;
        [Tooltip("航向转向滚转增益 Kp")]
        public float headingKp = 1.2f;

        public FDVerticalMode vMode = FDVerticalMode.OFF;
        public FDLateralMode lMode = FDLateralMode.OFF;
        public bool isFDOn = true;
        public float currentRWYHeading = 048;
        // 核心输出状态 (供 PFD 读取)
        [HideInInspector] public float fdVerNormalized = 0.5f; // 0.0(下) ~ 0.5(中) ~ 1.0(上)
        [HideInInspector] public float fdHorNormalized = 0.5f; // 0.0(左) ~ 0.5(中) ~ 1.0(右)

        [HideInInspector] public bool isPitchBarVisible = false;
        [HideInInspector] public bool isRollBarVisible = false;
        [HideInInspector] public bool isYawBarVisible = false;
        [HideInInspector] public bool isFPDMode = false;       // 是否为 TRK-FPA 绿鸟模式

        // 内部采样状态
        private VRCPlayerApi localPlayer;

        private float currentIAS;
        private float currentVertSpeed;
        private float currentPitch;
        private float currentRoll;
        private float currentHeading;
        private float currentAltitudeRA;
        private bool isGrounded;

        [HideInInspector] public float debugTargetPitch;
        [HideInInspector] public float debugCurrentPitch;
        [HideInInspector] public float debugTargetRoll;
        [HideInInspector] public float debugCurrentRoll;


        private void Start() {
            localPlayer = Networking.LocalPlayer;
        }

        public void UpdateFDLogic(float IAS, float vs ,float pitch, float roll, float heading, float altRA, bool grounded) {
            currentIAS = IAS;
            currentVertSpeed = vs;
            currentPitch = pitch;
            currentRoll = roll;
            currentHeading = heading;
            currentAltitudeRA = altRA;
            isGrounded = grounded;


            if (fcu == null || !isFDOn) {
                ResetFDOutputs();
                return;
            }

            // 读取 FCU 模式状态
            isFPDMode = fcu.isTrkFpaMode;

            // 1. 垂直模式解算
            switch (fcu.verticalMode) {
                case FCU.VerticalFlightMode.SRS: vMode = FDVerticalMode.SRS; break;
                case FCU.VerticalFlightMode.OP_CLB:
                case FCU.VerticalFlightMode.CLB: vMode = FDVerticalMode.OP_CLB; break;
                case FCU.VerticalFlightMode.ALT_HOLD: vMode = FDVerticalMode.ALT; break;
                case FCU.VerticalFlightMode.VS: vMode = FDVerticalMode.VS; break;
                case FCU.VerticalFlightMode.FPA: vMode = FDVerticalMode.FPA; break;
                case FCU.VerticalFlightMode.OP_DES:
                case FCU.VerticalFlightMode.DES: vMode = FDVerticalMode.OP_DES; break;
                default: vMode = FDVerticalMode.OFF; break;
            }

            // 2. 横向模式解算
            switch (fcu.lateralMode) {
                case FCU.LateralFlightMode.RWY: {
                        lMode = FDLateralMode.RWY;
                        fcu.targetHeading = currentRWYHeading;
                        break;
                    }
                case FCU.LateralFlightMode.RWY_TRK: lMode = FDLateralMode.RWY_TRK; break;
                case FCU.LateralFlightMode.HDG: lMode = isFPDMode ? FDLateralMode.TRK : FDLateralMode.HDG; break;
                case FCU.LateralFlightMode.NAV: lMode = FDLateralMode.NAV; break;
                default: lMode = FDLateralMode.OFF; break;
            }


            lMode = (isFPDMode) ? FDLateralMode.TRK : lMode;

            //还差 SRS 与 RWY RWY_TRK

            // 1. 计算纵向目标 (Pitch / FPA)
            float targetPitch = CalculateTargetPitch(vMode);

            // 2. 计算横向目标 (Roll / TRK)
            float targetRoll = CalculateTargetRoll(lMode);

            // 3. 计算偏差与归一化 [0.0, 1.0]
            float pitchError = targetPitch - currentPitch;
            float rollError = targetRoll - currentRoll;

            fdVerNormalized = Mathf.Clamp01(0.5f + (-pitchError / maxPitchDev) * 0.5f);
            fdHorNormalized = Mathf.Clamp01(0.5f + (rollError / maxRollDev) * 0.5f);

            // 暴露调试变量
            debugTargetPitch = targetPitch;
            debugCurrentPitch = currentPitch;
            debugTargetRoll = targetRoll;
            debugCurrentRoll = currentRoll;

            // 4. 控制显示显隐状态机 (空客阶段裁决)
            UpdateFDVisibilities(vMode, lMode);
        }

        private float CalculateTargetPitch(FDVerticalMode vMode) {
            switch (vMode) {
                case FDVerticalMode.SRS:
                    // 起飞 SRS 模式：维持 V2+10kt 姿态，基础给 15 度目标俯仰角
                    return 15.0f;

                case FDVerticalMode.ALT:
                    // 高度保持：根据高度差换算目标俯仰（此处以保持当前平飞姿态为简易计算）
                    return 0.0f;

                case FDVerticalMode.VS:
                    // V/S 模式：根据目标的垂直速度与当前真空速计算所需的俯仰角
                    float speedKts = Mathf.Max(currentIAS, 60.0f);
                    float targetVsFpm = fcu.targetVS;
                    // theta approx = arcsin(VS / TAS)
                    float targetPitchRad = Mathf.Asin(Mathf.Clamp((targetVsFpm * 0.00508f) / (speedKts * 0.51444f), -0.5f, 0.5f));
                    return targetPitchRad * Mathf.Rad2Deg;

                case FDVerticalMode.FPA:
                    // FPA 模式：目标轨迹角（绿鸟模式下直接作为垂直目标）
                    return fcu.targetFPA;

                case FDVerticalMode.OP_CLB:
                    return 12.5f;

                case FDVerticalMode.OP_DES:
                    return -5.0f;

                default:
                    return currentPitch;
            }
        }

        private float CalculateTargetRoll(FDLateralMode lMode) {
            switch (lMode) {
                case FDLateralMode.HDG:
                case FDLateralMode.RWY_TRK:
                case FDLateralMode.RWY:
                case FDLateralMode.TRK:
                    float targetHdg = fcu.targetHeading;
                    float hdgError = Mathf.DeltaAngle(currentHeading, targetHdg);
                    // P 比例计算目标坡度，限制最大坡度为 25 度
                    float targetBank = Mathf.Clamp(hdgError * headingKp, -25.0f, 25.0f);
                    return targetBank;


                default:
                    return currentRoll;
            }
        }

        private void UpdateFDVisibilities(FDVerticalMode vMode, FDLateralMode lMode) {
            // 地面滑跑：俯仰杆隐藏，横向杆/偏航杆显示
            if (isGrounded) {
                isPitchBarVisible = false;
                isRollBarVisible = (lMode != FDLateralMode.OFF);
                isYawBarVisible = (lMode == FDLateralMode.RWY);
                return;
            }

            // 着陆 Flare 阶段（小于 30ft RA）：自动隐藏 FD
            if (currentAltitudeRA < 30.0f && vMode != FDVerticalMode.SRS) {
                isPitchBarVisible = false;
                isRollBarVisible = false;
                isYawBarVisible = false;
                return;
            }

            // 常规空中飞行
            isYawBarVisible = false;
            isPitchBarVisible = (vMode != FDVerticalMode.OFF);
            isRollBarVisible = (lMode != FDLateralMode.OFF);
        }

        private void ResetFDOutputs() {
            fdVerNormalized = 0.5f;
            fdHorNormalized = 0.5f;
            isPitchBarVisible = false;
            isRollBarVisible = false;
            isYawBarVisible = false;
            isFPDMode = false;
        }
    }
}