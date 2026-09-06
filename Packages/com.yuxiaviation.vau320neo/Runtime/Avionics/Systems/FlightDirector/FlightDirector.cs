using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

using A320VAU.FCU;
using VRC.Core;
using A320VAU.ADIRU;
//using Codice.Client.Commands;
namespace A320VAU.Avionics {
    public enum FDVerticalMode { OFF, SRS, OP_CLB, ALT,ALT_STAR, VS, FPA, OP_DES }
    public enum FDLateralMode { OFF, RWY, RWY_TRK, HDG, NAV, TRK }

    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FlightDirector : UdonSharpBehaviour {
        [Header("--- References ---")]
        public FCU.FCU fcu;
        public FMGC.FMGC fmgc;
        [Header("--- Performance Tuning ---")]
        [Tooltip("FD 俯仰杆达到最大偏转所需的偏差角度(度)")]
        public float maxPitchDev = 15.0f;
        [Tooltip("FD 滚转杆达到最大偏转所需的偏差角度(度)")]
        public float maxRollDev = 30.0f;

        [Header("--- Controllor ---")]
        [Header("俯仰环")]
        public float altHoldKp = 0.001f;
        public float altHoldKi = 0.001f;

        public float altStarKp = 0.05f; //0.1 0.025 0.05

        [SerializeField] private float altDiff;
        [SerializeField] private float altDiffIntegrate = 0;

        [Header("速度环")]
        public float opKp = 1f; //用于控制开放爬升下降的比例参数
        [SerializeField] private float speedDiff;

        [Header("滚转环")]
        public float headingKp = 1.2f;

        [Header("工作状态")]
        public FCU.VerticalFlightMode vMode = FCU.VerticalFlightMode.None;
        public FCU.LateralFlightMode lMode = FCU.LateralFlightMode.None;

        public bool isFDOn = true;
        public float currentRWYHeading = 048;


        // 核心输出状态 (供 PFD 读取)
        public float pitchError = 0f;
        public float rollError = 0f;
        [HideInInspector] public float fdVerNormalized = 0.5f; // 0.0(下) ~ 0.5(中) ~ 1.0(上)
        [HideInInspector] public float fdHorNormalized = 0.5f; // 0.0(左) ~ 0.5(中) ~ 1.0(右)

        [HideInInspector] public bool isPitchBarVisible = false;
        [HideInInspector] public bool isRollBarVisible = false;
        [HideInInspector] public bool isYawBarVisible = false;
        [HideInInspector] public bool isFPDMode = false;       // 是否为 TRK-FPA 绿鸟模式
        public float targetRoll;
        public float targetPitch;
        

        // 内部采样状态
        private VRCPlayerApi localPlayer;

        [SerializeField] private float currentIAS;
        [SerializeField] private float currentVertSpeed;
        [SerializeField] private float currentPitch;
        [SerializeField] private float currentRoll;
        [SerializeField] private float currentTrackPitch;
        [SerializeField] private float currentTrackBank;
        [SerializeField] private float currentHeading;
        [SerializeField] private float currentAltitudeRA;
        [SerializeField] private float currentAltitude;
        [SerializeField] private bool isGrounded;

        [HideInInspector] public float debugTargetPitch;
        [HideInInspector] public float debugCurrentPitch;
        [HideInInspector] public float debugTargetRoll;
        [HideInInspector] public float debugCurrentRoll;
        
        

        private void Start() {
            localPlayer = Networking.LocalPlayer;
        }

        public void UpdateFDLogic(float IAS, float vs ,float pitch, float roll, float trackPitch, float trackBank,
            float heading, float PressureAltitude,float altRA, bool grounded) {
            currentIAS = IAS;
            currentVertSpeed = vs;
            currentPitch = pitch;
            currentRoll = roll;
            currentTrackPitch = trackPitch;
            currentTrackBank = trackBank;

            currentHeading = heading;
            currentAltitudeRA = altRA;
            currentAltitude = PressureAltitude;
            isGrounded = grounded;

            if (fcu == null || !isFDOn) {
                ResetFDOutputs();
                return;
            }

            // 读取 FCU 模式状态
            isFPDMode = fcu.isTrkFpaMode;

            // 1. 垂直模式解算
            vMode = fcu.verticalMode;

            // 2. 横向模式解算
            lMode = fcu.lateralMode;
            switch (lMode) {
                case FCU.LateralFlightMode.RWY: {
                        fcu.targetHeading = currentRWYHeading;
                        break;
                    }
            }

            //还差 SRS 与 RWY RWY_TRK

            // 1. 计算纵向目标 (Pitch / FPA)
            targetPitch = CalculateTargetPitch(vMode);

            // 2. 计算横向目标 (Roll / TRK)
            targetRoll = CalculateTargetRoll(lMode);
            

            // 3. 计算偏差与归一化 [0.0, 1.0]
            float pitchErrorNext = targetPitch - currentPitch;
            float rollErrorNext = targetRoll - currentRoll;

            // 4. 滤波
            pitchError = Mathf.MoveTowards(pitchError, pitchErrorNext, 3*Time.deltaTime);
            rollError = Mathf.MoveTowards(rollError, rollErrorNext, 3 * Time.deltaTime);

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

        private float CalculateTargetPitch(VerticalFlightMode vMode) {
            float targetPitchDeg = 0f;

            switch (vMode) {
                case VerticalFlightMode.SRS:
                    // 起飞 SRS 模式：维持 V2+10kt 姿态，基础给 15 度目标俯仰角
                    //return 15.0f;
                    speedDiff = currentIAS - (fmgc.performance.v2 + 10);
                    targetPitchDeg = Mathf.Clamp(speedDiff * 0.5f, -10f, 5f) + 15; // 1
                    return targetPitchDeg;

                case VerticalFlightMode.ALT_HOLD:
                    // 高度保持：根据高度差换算目标俯仰（此处以保持当前平飞姿态为简易计算）
                    //return 0.0f + currentTrackPitch;
                    /*不要尝试使用比例控制器控制俯仰轴，会震荡*/
                    //targetPitchDeg = Mathf.Clamp(-currentVertSpeed * altHoldKp, -5f, 5f); //0.2 0.05 0.003 0.0003 0.001
                    //return targetPitchDeg;
                    altDiff = fcu.targetAltitude + 45 - currentAltitude; //+45,比较粗暴的削稳态误差的方法
                    altDiffIntegrate = Mathf.Clamp(altDiffIntegrate + altDiff * Time.deltaTime, -10, 10);
                    targetPitchDeg = Mathf.Clamp(altDiff * altHoldKp + altDiffIntegrate * altHoldKi, -5.0f, 12.5f);// - currentTrackPitch; 
                    return targetPitchDeg;

                case VerticalFlightMode.ALT_STAR:
                    
                    // 高度捕获，计算一个柔和的剖面
                    altDiff = fcu.targetAltitude + 45 - currentAltitude;
                    altDiffIntegrate = 0f; //因为进入高度模式前一定要经过alt star,所以在这里清空高度误差的积分
                    // 使用比例增益将高度差转换为目标俯仰角（实现抛物线拉平）
                    // 250ft 时 Pitch 约为 5°，随着高度差归零，Pitch 平滑收敛至 0°（平飞）
                    targetPitchDeg = Mathf.Clamp(altDiff * altStarKp, -5.0f, 12.5f);// - currentTrackPitch; 
                    // 将计算好的俯仰目标赋予 FD 纵向偏转量
                    return targetPitchDeg;

                case FCU.VerticalFlightMode.VS:
                    // V/S 模式：根据目标的垂直速度与当前真空速计算所需的俯仰角
                    float speedKts = Mathf.Max(currentIAS, 60.0f);
                    float targetVsFpm = fcu.targetVS;
                    // theta approx = arcsin(VS / TAS)
                    float targetPitchRad = Mathf.Asin(Mathf.Clamp((targetVsFpm * 0.00508f) / (speedKts * 0.51444f), -0.5f, 0.5f));
                    //return targetPitchRad * Mathf.Rad2Deg - currentTrackPitch; V/S模式考虑航迹角时震荡有点严重
                    return targetPitchRad * Mathf.Rad2Deg;

                case VerticalFlightMode.FPA:
                    // FPA 模式：目标轨迹角（绿鸟模式下直接作为垂直目标）
                    return fcu.targetFPA;

                case FCU.VerticalFlightMode.OP_CLB:
                    speedDiff = currentIAS - (fcu.targetSpeed);
                    targetPitchDeg = Mathf.Clamp(speedDiff * opKp, 5f, 20f);  
                    //targetPitchDeg = 12.5f;
                    return targetPitchDeg;

                case VerticalFlightMode.OP_DES:
                    speedDiff = currentIAS - (fcu.targetSpeed);
                    targetPitchDeg = Mathf.Clamp(speedDiff * opKp, -10f, 0f); 
                    //targetPitchDeg = -5f;
                    return targetPitchDeg;

                case FCU.VerticalFlightMode.CLB:
                    targetPitchDeg = 12.5f;
                    return targetPitchDeg;

                case FCU.VerticalFlightMode.DES:
                    targetPitchDeg = -5f;
                    return targetPitchDeg;
                    
                default:
                    //return currentPitch;
                    return 0f;
            }
        }

        private float CalculateTargetRoll(LateralFlightMode lMode) {
            switch (lMode) {
                case LateralFlightMode.HDG:
                case LateralFlightMode.RWY_TRK:
                case LateralFlightMode.RWY:
                case LateralFlightMode.TRK:
                    float targetHdg = fcu.targetHeading;
                    float hdgError = Mathf.DeltaAngle(currentHeading, targetHdg) - currentTrackBank;//试验一下正负
                    // P 比例计算目标坡度，限制最大坡度为 25 度
                    float targetBank = Mathf.Clamp(hdgError * headingKp, -25.0f, 25.0f);
                    return targetBank;


                default:
                    return currentRoll;
            }
        }

        private void UpdateFDVisibilities(VerticalFlightMode vMode, LateralFlightMode lMode) {
            // 地面滑跑：俯仰杆隐藏，横向杆/偏航杆显示
            if (isGrounded) {
                isPitchBarVisible = false;
                isRollBarVisible = (lMode != LateralFlightMode.None);
                isYawBarVisible = (lMode == LateralFlightMode.RWY);
                return;
            }

            // 着陆 Flare 阶段（小于 30ft RA）：自动隐藏 FD
            if (currentAltitudeRA < 30.0f && vMode != VerticalFlightMode.SRS) {
                isPitchBarVisible = false;
                isRollBarVisible = false;
                isYawBarVisible = false;
                return;
            }

            // 常规空中飞行
            isYawBarVisible = false;
            isPitchBarVisible = (vMode != VerticalFlightMode.None);
            isRollBarVisible = (lMode != LateralFlightMode.None);
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