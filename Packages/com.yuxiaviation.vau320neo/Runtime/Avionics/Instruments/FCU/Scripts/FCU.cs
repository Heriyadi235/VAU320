using System;
using A320VAU.Avionics;
using A320VAU.Common;
using A320VAU.FCU;
using A320VAU.PFD;
using A320VAU.SFEXT;
using A320VAU.Utils;
using Avionics.Systems.Common;
using EsnyaSFAddons.Weather;
using SaccFlightAndVehicles;
using TMPro;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A320VAU.FCU {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]//FCU会同步，FD与AP不会同步，按键事件会被发送给owner
    public class FCU : UdonSharpBehaviour {
        public FMAController fmaController1;
        public FMAController fmaController2;
        public PFDBasicDisplay PFD_PF;
        public PFDBasicDisplay PFD_PM;
        private DFUNC_AltHold _altHoldDFunc;
        private DFUNC_a320_AutoThrust _ATHRDFunc;
        private DependenciesInjector _injector;
        private ADIRU.ADIRU _adiru;
        [SerializeField] private AircraftSystemData _aircraftSystemData;

        private readonly float UPDATE_INTERVAL = UpdateIntervalUtil.GetUpdateIntervalFromFPS(5);
        private float _lastUpdate;

        [SerializeField] private float _current_altitude = 0f;

        [Header("--- SPEED / MACH WINDOW ---")]
        public float targetSpeed = 250f;            // 节(Knots) 或 Mach
        public bool isMachMode = false;             // 是否为Mach显示
        public GuidanceMode speedGuidance = GuidanceMode.Managed;

        [Header("--- LATERAL WINDOW (HDG/TRK) ---")]
        public float targetHeading = 360f;          // 航向 (0-360)
        public bool isTrkFpaMode = false;           // 是否处于 TRK/FPA 模式
        public GuidanceMode lateralGuidance = GuidanceMode.Managed;
        //public LateralFlightMode lateralMode = LateralFlightMode.NAV;
        public LateralFlightMode lateralMode = LateralFlightMode.None;
        [Header("--- VERTICAL WINDOW (ALT & VS) ---")]
        public float preSelectAltitude = 3000f;
        public float targetAltitude = 3000f;       // 目标高度 (ft)
        public float targetVS = 0f;                 // 垂直速度 (ft/min)
        public float targetFPA = 0f;                // 飞行轨迹角 (度)
        public int altitudeStep = 1000;             // 高度增量 (100 或 1000)
        public GuidanceMode verticalGuidance = GuidanceMode.Managed;
        //public VerticalFlightMode verticalMode = VerticalFlightMode.ALT_HOLD;
        public VerticalFlightMode verticalMode = VerticalFlightMode.None;

        [Header("--- AP & ATHR & APPR STATUS ---")]
        public bool isAP1Active = false;
        public bool isAP2Active = false;
        public bool isFD1Active = true;
        public bool isFD2Active = true;
        public bool isATHRActive = false;
        public ApprModeStatus locStatus = ApprModeStatus.Off;
        public ApprModeStatus apprStatus = ApprModeStatus.Off;
        public bool isExpedActive = false;

        #region UI Elements
        private readonly int AP1_HASH = Animator.StringToHash("IsAP1On");
        private readonly int AP2_HASH = Animator.StringToHash("IsAP2On");
        private readonly int ATHR_HASH = Animator.StringToHash("IsAutoThrustOn");
        [Header("--- UI TEXT COMPONENTS (Safe Null Handled) ---")]
        public Animator cockpitAnimator;
        // 兼容 Legacy Text
        public Text SpeedText;
        public Text HeadingText;
        public Text AltitudeText;
        public Text VerticalSpeedText;

        public TextMeshProUGUI SpeedTextTMP;           // 速度显示文本
        public TextMeshProUGUI HeadingTextTMP;         // 航向显示文本
        public TextMeshProUGUI AltitudeTextTMP;        // 高度显示文本
        public TextMeshProUGUI VerticalSpeedTextTMP;              // 垂直速度显示文本

        [Header("--- UI LIGHTS & DOTS (Safe Null Handled) ---")]
        public GameObject SpeedModeIndicate;
        public GameObject MachModeIndicate;
        public GameObject SpeedManagedIndicate; // 速度管理圆点

        public GameObject HeadingModeIndicate;
        public GameObject TrackModeIndicate;
        public GameObject GPSModeIndicate;
        public GameObject HeadingManagedIndicate; // 航向管理圆点 (NAV Dot)

        public GameObject HDGVSModeIndicate1;// HDG/VS 指示
        public GameObject HDGVSModeIndicate2;// HDG/VS 指示

        public GameObject TRKFPAModeIndicate1; // TRK/FPA 指示
        public GameObject TRKFPAModeIndicate2; // TRK/FPA 指示

        public GameObject VerticalSpeedManagedIndicate;// 高度管理圆点
        public GameObject VerticalSpeedModeIndicate;
        public GameObject FPAModeIndicate; // TRK/FPA 指示

        public GameObject ap1Light;                 // AP1 按钮灯
        public GameObject ap2Light;                 // AP2 按钮灯
        public GameObject fd1Light;
        public GameObject fd2Light;
        public GameObject athrLight;                // A/THR 按钮灯
        public GameObject locLight;                 // LOC 按钮灯
        public GameObject apprLight;                // APPR 按钮灯
        public GameObject expedLight;               // EXPED 按钮灯

        #endregion

        // ----------------------------------------------------
        // FCU 按钮与旋钮事件接口 (供VR手柄/Inspector按钮调用)
        // ----------------------------------------------------

        #region Speed Knob Events
        public void TurnSpeedKnob(float delta) {
            if (isMachMode)
                targetSpeed = Mathf.Clamp(targetSpeed + delta * 0.01f, 0.10f, 0.99f);
            else
                targetSpeed = Mathf.Clamp(targetSpeed + delta, 100f, 390f);
            
            _ATHRDFunc.SetSpeed = Convert.ToInt32(targetSpeed / 1.9438445f);
        }
        public void PushSpeedKnob() {
            speedGuidance = GuidanceMode.Managed;
        }
        public void PullSpeedKnob() {
            speedGuidance = GuidanceMode.Selected;
        }
        public void TurnSpeedKnobPlus10() => TurnSpeedKnob(10);
        public void TurnSpeedKnobPlus1() => TurnSpeedKnob(1);
        public void TurnSpeedKnobMinus10() => TurnSpeedKnob(-10);
        public void TurnSpeedKnobMinus1() => TurnSpeedKnob(-1);
        public void ToggleSpdMach() {
            isMachMode = !isMachMode;
            // 单位转换逻辑示例
            if (isMachMode) targetSpeed = 0.78f;
            else targetSpeed = 290f;
        }
        #endregion

        #region Heading Knob Events
        public void TurnHeadingKnob(float delta) {
            targetHeading = (targetHeading + delta) % 360f;
            if (targetHeading < 0) targetHeading += 360f;
        }

        public void PushHeadingKnob() {
            lateralGuidance = GuidanceMode.Managed;
            lateralMode = LateralFlightMode.NAV;
            if (apprStatus == ApprModeStatus.Engaged) apprStatus = ApprModeStatus.Off;
            if (locStatus == ApprModeStatus.Engaged) locStatus = ApprModeStatus.Off;
        }

        public void TurnHeadingKnobPlus10() => TurnHeadingKnob(10);
        public void TurnHeadingKnobPlus1() => TurnHeadingKnob(1);
        public void TurnHeadingKnobMinus10() => TurnHeadingKnob(-10);
        public void PushHeadingKnobMinus1() => TurnHeadingKnob(-1);

        public void PullHeadingKnob() {
            lateralGuidance = GuidanceMode.Selected;
            lateralMode = LateralFlightMode.HDG;
            if (apprStatus == ApprModeStatus.Engaged) apprStatus = ApprModeStatus.Off;
            if (locStatus == ApprModeStatus.Engaged) locStatus = ApprModeStatus.Off;
        }

        public void ToggleHdgTrkMode() {
            isTrkFpaMode = !isTrkFpaMode;
        }
        #endregion

        #region Altitude & VS Knob Events
        public void TurnAltitudeKnob(float delta) {
            preSelectAltitude = Mathf.Clamp(preSelectAltitude + delta * altitudeStep, 100f, 49000f);

            if (verticalMode != VerticalFlightMode.ALT_HOLD && verticalMode != VerticalFlightMode.ALT_STAR) {
                targetAltitude = preSelectAltitude;

                if (verticalMode != VerticalFlightMode.VS) {
                    if (verticalGuidance == GuidanceMode.Selected) {
                        verticalMode = (targetAltitude >= _current_altitude) ? VerticalFlightMode.OP_CLB : VerticalFlightMode.OP_DES;
                        isExpedActive = false;
                    }
                    else {
                        verticalMode = (targetAltitude >= _current_altitude) ? VerticalFlightMode.CLB : VerticalFlightMode.DES;
                    }
                }

            }
        }
        
        public void ToggleAltitudeStep() {
            altitudeStep = (altitudeStep == 1000) ? 100 : 1000;
        }

        public void PushAltitudeKnob() {
            verticalGuidance = GuidanceMode.Managed;
            targetAltitude = preSelectAltitude;
            isExpedActive = false;
            if (Mathf.Abs(_current_altitude - targetAltitude) > 200) { 
            verticalMode = (targetAltitude >= _current_altitude ) ? VerticalFlightMode.CLB : VerticalFlightMode.DES;
            
            }
        }

        public void PullAltitudeKnob() {
            verticalGuidance = GuidanceMode.Selected;
            targetAltitude = preSelectAltitude;
            isExpedActive = false;
            if (Mathf.Abs(_current_altitude - targetAltitude) > 200) {
                verticalMode = (targetAltitude >= _current_altitude) ? VerticalFlightMode.OP_CLB : VerticalFlightMode.OP_DES;
            }
        }

        public void PushVSKnobToLevelOff() {
            verticalGuidance = GuidanceMode.Selected;
            verticalMode = VerticalFlightMode.VS;
            targetVS = 0f;
            targetFPA = 0f;
        }

        public void PullVSKnob() {
            verticalGuidance = GuidanceMode.Selected;
            targetAltitude = preSelectAltitude;
            verticalMode = isTrkFpaMode ? VerticalFlightMode.FPA : VerticalFlightMode.VS;
        }

        public void TurnVSKnob(float delta) {
            if (isTrkFpaMode)
                targetFPA = Mathf.Clamp(targetFPA + delta * 0.1f, -9.9f, 9.9f);
            else
                targetVS = Mathf.Clamp(targetVS + delta * 100f, -6000f, 6000f);
        }

        public void TurnAltitudeKnobPlus1k() {
            TurnAltitudeKnob(1);
        }

        public void TurnAltitudeKnobMinus1k() {
            TurnAltitudeKnob(-1);
        }
        public void TurnAltitudeKnobPlus1h() {
            TurnAltitudeKnob(0.1f);
        }

        public void TurnAltitudeKnobMinus1h() {
            TurnAltitudeKnob(-0.1f);
        }

        public void TurnVSKnoblus5h() => TurnVSKnob(5);
        public void TurnVSKnobPlus1h() => TurnVSKnob(1);
        public void TurnVSKnobMinus5h() => TurnVSKnob(-5);
        public void TurnVSKnobbMinus1h() => TurnVSKnob(-1);

        #endregion

        #region FCU Buttons (AP, ATHR, APPR, LOC, EXPED)
        public void PressAP1() {
            // 1. 地面保护 / 姿态保护：地面禁止接通 AP（离地后才允许）
            if (_aircraftSystemData.isAircraftGrounded) {
                isAP1Active = false;
                return; 
            }

            // 2. 如果 AP1 当前已经是接通状态 -> 执行【关断】逻辑
            if (isAP1Active) {
                isAP1Active = false;
                // 此处可触发 AP 脱开音响或主警告逻辑
                return;
            }

            // 3. 如果 AP1 当前是关闭状态 -> 执行【接通】逻辑
            // 判断是否满足双 AP 共存条件：进近模式处于 Armed 或 Active
            bool isApprModeAvailable = (apprStatus == ApprModeStatus.Armed || apprStatus == ApprModeStatus.Engaged);

            if (!isApprModeAvailable) {
                // 非进近模式下，AP1 与 AP2 严格互斥：接通 AP1 必须强行断开 AP2
                isAP2Active = false;
            }

            // 接通 AP1
            isAP1Active = true;
        }

        public void PressAP2() {
            // 1. 地面保护 / 姿态保护：地面禁止接通 AP（离地后才允许）
            if (_aircraftSystemData.isAircraftGrounded) {
                isAP2Active = false;
                return;
            }

            // 2. 如果 AP1 当前已经是接通状态 -> 执行【关断】逻辑
            if (isAP2Active) {
                isAP2Active = false;
                // 此处可触发 AP 脱开音响或主警告逻辑
                return;
            }

            // 3. 如果 AP1 当前是关闭状态 -> 执行【接通】逻辑
            // 判断是否满足双 AP 共存条件：进近模式处于 Armed 或 Active
            bool isApprModeAvailable = (apprStatus == ApprModeStatus.Armed || apprStatus == ApprModeStatus.Engaged);

            if (!isApprModeAvailable) {
                // 非进近模式下，AP1 与 AP2 严格互斥：接通 AP1 必须强行断开 AP2
                isAP1Active = false;
            }

            // 接通 AP1
            isAP2Active = true;
        }

        public void PressATHR() {
            
            isATHRActive = !isATHRActive;
        }

        public void PressLOC() {
            if (locStatus == ApprModeStatus.Off) {
                locStatus = ApprModeStatus.Armed;
            }
            else {
                locStatus = ApprModeStatus.Off;
                if (lateralMode == LateralFlightMode.LOC) lateralMode = LateralFlightMode.HDG;
            }
        }

        public void PressAPPR() {
            if (apprStatus == ApprModeStatus.Off) {
                apprStatus = ApprModeStatus.Armed;
                locStatus = ApprModeStatus.Armed; // APPR 自动包含 LOC 预置
            }
            else {
                apprStatus = ApprModeStatus.Off;
                locStatus = ApprModeStatus.Off;
                if (verticalMode == VerticalFlightMode.GS) verticalMode = VerticalFlightMode.ALT_HOLD;
                if (lateralMode == LateralFlightMode.LOC) lateralMode = LateralFlightMode.HDG;
            }
        }

        public void PressEXPED() {
            isExpedActive = !isExpedActive;
            if (isExpedActive) {
                verticalGuidance = GuidanceMode.Selected;
                verticalMode = VerticalFlightMode.EXPED;
            }
        }

        #endregion


        public void UpdateFCUDisplay() {
            // 1. 速度窗口文本
            string spdStr = (speedGuidance == GuidanceMode.Managed) ? "---" :
                            (isMachMode ? targetSpeed.ToString("F2") : Mathf.RoundToInt(targetSpeed).ToString("D3"));
            SetText(SpeedTextTMP, SpeedText, spdStr);

            // 2. 航向窗口文本
            string hdgStr = (lateralGuidance == GuidanceMode.Managed) ? "---" : Mathf.RoundToInt(targetHeading).ToString("D3");
            SetText(HeadingTextTMP, HeadingText, hdgStr);

            // 3. 高度窗口文本
            string altStr = Mathf.RoundToInt(preSelectAltitude).ToString("D5");
            SetText(AltitudeTextTMP, AltitudeText, altStr);

            // 4. 垂直速度窗口文本
            string vsStr = "";
            //if (verticalGuidance == GuidanceMode.Managed && verticalMode != VerticalFlightMode.VS) {
            if (verticalMode != VerticalFlightMode.VS) {
                vsStr = "-----";
            }
            else {
                if (isTrkFpaMode)
                    vsStr = (targetFPA >= 0 ? "+" : "") + targetFPA.ToString("F1");
                else
                    vsStr = (targetVS >= 0 ? "+" : "") + Mathf.RoundToInt(targetVS).ToString("D4");
            }
            SetText(VerticalSpeedTextTMP, VerticalSpeedText, vsStr);

            // 5. 点亮/熄灭各种指示灯与Managed Dot圆点
            SetActiveSafe(SpeedManagedIndicate, speedGuidance == GuidanceMode.Managed);
            SetActiveSafe(HeadingManagedIndicate, lateralGuidance == GuidanceMode.Managed);
            SetActiveSafe(VerticalSpeedManagedIndicate, verticalGuidance == GuidanceMode.Managed);


            SetActiveSafe(ap1Light, isAP1Active);
            SetActiveSafe(ap2Light, isAP2Active);
            SetActiveSafe(athrLight, isATHRActive);
            SetActiveSafe(locLight, locStatus != ApprModeStatus.Off);
            SetActiveSafe(apprLight, apprStatus != ApprModeStatus.Off);
            SetActiveSafe(expedLight, isExpedActive);

            SetActiveSafe(HDGVSModeIndicate1, !isTrkFpaMode);
            SetActiveSafe(HDGVSModeIndicate2, !isTrkFpaMode);

            SetActiveSafe(TRKFPAModeIndicate1, isTrkFpaMode);
            SetActiveSafe(TRKFPAModeIndicate2, isTrkFpaMode);
        }

        // 安全设定 Text 组件的辅助函数
        private void SetText(TextMeshProUGUI tmpText, Text legacyText, string value) {
            if (tmpText != null) tmpText.text = value;
            if (legacyText != null) legacyText.text = value;
        }

    // 安全设定 GameObject 显隐状态的辅助函数
        private void SetActiveSafe(GameObject obj, bool active) {
            if (obj != null && obj.activeSelf != active) {
                obj.SetActive(active);
            }
        }

        //设定FMA显示
        private void SyncToFMA(FMAController fmaController) {
            if (fmaController == null) return;

            // 1. 同步 AP & FD & ATHR 激活状态
            fmaController.IsAutoPilot1Active = isAP1Active;
            fmaController.IsAutoPilot2Active = isAP2Active;
            fmaController.IsFlightDirector1Active = isFD1Active;
            fmaController.IsFlightDirector2Active = isFD2Active;
            fmaController.IsAutoThrustActive = isATHRActive;
            // 2. 将纵向模式映射为 FMA 文本
            //激活
            switch (verticalMode) {
                case VerticalFlightMode.None:
                    fmaController.VerticalActiveMode = "";
                    break;
                case VerticalFlightMode.ALT_HOLD:
                    fmaController.VerticalActiveMode = "ALT";
                    break;
                case VerticalFlightMode.ALT_STAR:
                    fmaController.VerticalActiveMode = "ALT*";
                    break;
                case VerticalFlightMode.OP_CLB:
                    fmaController.VerticalActiveMode = "OP CLB";
                    break;
                case VerticalFlightMode.OP_DES:
                    fmaController.VerticalActiveMode = "OP DES";
                    break;
                case VerticalFlightMode.SRS:
                    fmaController.VerticalActiveMode = "SRS";
                    break;

                case VerticalFlightMode.CLB:
                    fmaController.VerticalActiveMode = "CLB";
                    break;

                case VerticalFlightMode.DES:
                    fmaController.VerticalActiveMode = "DES";
                    break;

                case VerticalFlightMode.EXPED:
                    fmaController.VerticalActiveMode = "EXPED";
                    break;
                case VerticalFlightMode.VS:
                    fmaController.VerticalActiveMode = "V/S " + targetVS.ToString("+0000;-0000;+0000"); ;
                    break;
                case VerticalFlightMode.GS:
                    fmaController.VerticalActiveMode = "G/S";
                    break;
                case VerticalFlightMode.FPA:
                    fmaController.VerticalActiveMode = "FPA " + targetFPA.ToString("+0.0;-0.0;+0.0") + "°";
                    break;
                default:
                    fmaController.VerticalActiveMode = "";
                    break;
            }
            //预位
            fmaController.VerticalArmMode = GetVerticalArmedMode();


            // 3. 将横向模式映射为 FMA 文本
            //激活
            switch (lateralMode) {
                case LateralFlightMode.None:
                    fmaController.LateralActiveMode = "";
                    break;
                case LateralFlightMode.RWY_TRK:
                    fmaController.LateralActiveMode = "RWY TRK";
                    break;
                case LateralFlightMode.HDG:
                    fmaController.LateralActiveMode = isTrkFpaMode ? "TRK" : "HDG";
                    break;

                case LateralFlightMode.NAV:
                    fmaController.LateralActiveMode = "NAV";
                    break;

                case LateralFlightMode.LOC:
                    fmaController.LateralActiveMode = "LOC";
                    break;

                case LateralFlightMode.LAND:
                    fmaController.LateralActiveMode = "LAND";
                    break;

                case LateralFlightMode.RWY:
                    fmaController.LateralActiveMode = "RWY";
                    break;

                case LateralFlightMode.GA_TRK:
                    fmaController.LateralActiveMode = "GA TRK";
                    break;

                default:
                    fmaController.LateralActiveMode = "";
                    break;
            }

            // 预位
            fmaController.LateralArmMode = GetLateralArmedMode();

            //4.推力模式
            if (_ATHRDFunc.Cruise || _ATHRDFunc.isAutoThrustArm) {
                fmaController.IsAutoThrustActive = true;
                fmaController.IsAutoThrustArm = _ATHRDFunc.isAutoThrustArm;

                if (_ATHRDFunc.Cruise) {
                    if(verticalMode == VerticalFlightMode.OP_CLB)
                        fmaController.AutoThrustMode = "THR CLB";
                    else if (verticalMode == VerticalFlightMode.OP_DES)
                        fmaController.  AutoThrustMode = "THR IDLE";
                    else
                        fmaController.AutoThrustMode = "SPEED";
                }
                else {
                    fmaController.AutoThrustMode = "";
                }
            }
            else {
                fmaController.IsAutoThrustActive = false;
                fmaController.AutoThrustMode = "";
            }

        }
        
        private void Start() {
            _injector = DependenciesInjector.GetInstance(this);
            _ATHRDFunc = _injector.autoThrust;
            _altHoldDFunc = _injector.altHold;
            _adiru = _injector.adiru;

        }

        private void LateUpdate() {
            if (!UpdateIntervalUtil.CanUpdate(ref _lastUpdate, UPDATE_INTERVAL)) return;
            targetSpeed = Convert.ToInt32(_ATHRDFunc.SetSpeed * 1.9438445f);
            _current_altitude = _adiru.adr.pressureAltitude;
            isATHRActive = _ATHRDFunc.Cruise;
            CheckAltitudeCapture();
            isFD1Active = PFD_PF.isFlightDirectionOn;
            isFD2Active = PFD_PM.isFlightDirectionOn;
            UpdateFCUDisplay();
            SyncToFMA(fmaController2);
            SyncToFMA(fmaController1);
        }

        public void ResetFCU() {
            // 1. 重置速度/马赫窗口数值与模式
            targetSpeed = 250f;
            isMachMode = false;
            speedGuidance = GuidanceMode.Managed;

            // 2. 重置横向窗口 (HDG/TRK) 数值与模式
            targetHeading = 360f;
            isTrkFpaMode = false;
            lateralGuidance = GuidanceMode.Managed;
            lateralMode = LateralFlightMode.None;

            // 3. 重置纵向窗口 (ALT/VS) 数值与模式
            preSelectAltitude = 3000f;
            targetAltitude = 3000f;
            targetVS = 0f;
            targetFPA = 0f;
            altitudeStep = 1000;
            verticalGuidance = GuidanceMode.Managed;
            verticalMode = VerticalFlightMode.None;

            // 4. 断开所有自动飞行与进近系统 (AP / FD / ATHR / APPR)
            isAP1Active = false;
            isAP2Active = false;
            isFD1Active = true;  // 上电默认开启 FD
            isFD2Active = true;
            isATHRActive = false;
            locStatus = ApprModeStatus.Off;
            apprStatus = ApprModeStatus.Off;
            isExpedActive = false;

            // 5. 立即刷新硬件与 FMA 显示面板
            UpdateFCUDisplay();
            SyncToFMA(fmaController1);
            SyncToFMA(fmaController2);
        }
        private string GetVerticalArmedMode() {
            // 优先级 1：盲降/进近预位（按下 APPR 且未截获 G/S）
            if (apprStatus == ApprModeStatus.Armed && verticalMode != VerticalFlightMode.GS) {
                return "G/S";
            }

            // 优先级 2：起飞/复飞 SRS 阶段（根据引导模式预位 CLB 或 巡航/爬升高度）
            if (verticalMode == VerticalFlightMode.SRS) {
                bool isClimb = targetAltitude >= _current_altitude && Mathf.Abs(_current_altitude - targetAltitude) > 100f;

                if (verticalGuidance == GuidanceMode.Selected)
                    return isClimb ? "OP CLB" : "OP DES"; // 修正下划线为标准 FMA 字符串格式
                else
                    return isClimb ? "CLB" : "DES";
            }

            // 优先级 3：常规爬升/下降/平飞阶段，若未处于 ALT/ALT* 或 G/S 阶段，自动预位 ALT
            if (verticalMode != VerticalFlightMode.ALT_HOLD &&
                verticalMode != VerticalFlightMode.ALT_STAR &&
                verticalMode != VerticalFlightMode.GS) {
                return "ALT";
            }

            // 默认无预位显示
            return "";
        }

        private string GetLateralArmedMode() {
            // 1. 航向道/盲降截获预位（按下 APPR/LOC 且尚未截获 LOC）
            if (locStatus == ApprModeStatus.Armed && lateralMode != LateralFlightMode.LOC) {
                return "LOC";
            }

            // 2. 导航预位：托管模式下，且当前未进入 LOC、LAND 或已经截获 NAV 的阶段
            if (lateralGuidance == GuidanceMode.Managed &&
                lateralMode != LateralFlightMode.NAV &&
                lateralMode != LateralFlightMode.LOC &&
                lateralMode != LateralFlightMode.LAND) {
                return "NAV";
            }

            // 3. 默认无预位
            return "";
        }
        
        private void CheckAltitudeCapture() {
            // 已在非高度控制模式下跳过检测
            if (
                verticalMode == VerticalFlightMode.GS || verticalMode == VerticalFlightMode.ALT_HOLD ||
                verticalMode == VerticalFlightMode.SRS) return;

            if (verticalMode == VerticalFlightMode.OP_CLB) { 
                _ATHRDFunc.OP_CLB = true;
                _ATHRDFunc.OP_DES = false;
            }
            else if (verticalMode == VerticalFlightMode.OP_DES) {
                _ATHRDFunc.OP_CLB = false;
                _ATHRDFunc.OP_DES = true;
            }
            else {
                _ATHRDFunc.OP_CLB = false;
                _ATHRDFunc.OP_DES = false;

            }
            float altDiff = Mathf.Abs(_current_altitude - targetAltitude);
            

            // 当接近目标高度（例如 50 英尺以内）时自动平飞切入 ALT_HOLD
            // 1. 接近目标高度（如 250 英尺以内）：切入 ALT* (ALT Star) 捕获模式
            if (altDiff <= 250f && altDiff > 100f) {
                if (verticalMode != VerticalFlightMode.ALT_STAR) {
                    verticalMode = VerticalFlightMode.ALT_STAR;
                }
            }
            // 2. 高度完全稳定（20 英尺以内）：由 ALT* 转换为 ALT_HOLD 保持模式
            else if (altDiff <= 50f && verticalMode == VerticalFlightMode.ALT_STAR) {
                verticalMode = VerticalFlightMode.ALT_HOLD;
                targetVS = 0f;
                targetFPA = 0f;
                isExpedActive = false;
            }

        }

        /*
            #region Property
                [FieldChangeCallback(nameof(FCUMode))] public FCUMode _fcuMode = FCUMode.HeadingVerticalSpeed;
                public FCUMode FCUMode {
                    get => _fcuMode;
                    set {
                        _fcuMode = value;
                        UpdateFCUMode();
                    }
                }

                [FieldChangeCallback(nameof(IsSpeedManaged))]
                public bool _isSpeedManaged;

                public bool IsSpeedManaged {
                    get => _isSpeedManaged;
                    set {
                        _isSpeedManaged = value;
                        UpdateSpeedWindow();
                    }
                }

                [FieldChangeCallback(nameof(TargetSpeed))]
                public int _targetSpeed = 100;

                public int TargetSpeed {
                    get => _targetSpeed;
                    set {
                        _targetSpeed = value;
                        UpdateSpeedWindow();
                    }
                }

                [FieldChangeCallback(nameof(IsMachSpeed))]
                public bool _isMachSpeed;

                public bool IsMachSpeed {
                    get => _isMachSpeed;
                    set {
                        _isMachSpeed = value;
                        UpdateSpeedWindow();
                    }
                }

                [FieldChangeCallback(nameof(TargetMach))]
                public double _targetMach = 0.6;

                public double TargetMach {
                    get => _targetMach;
                    set {
                        _targetMach = value;
                        UpdateSpeedWindow();
                    }
                }

                [FieldChangeCallback(nameof(IsHeadingManaged))]
                public bool _isHeadingManaged;

                public bool IsHeadingManaged {
                    get => _isHeadingManaged;
                    set {
                        _isHeadingManaged = value;
                        UpdateHeadingWindow();
                    }
                }

                [FieldChangeCallback(nameof(TargetHeading))]
                public int _targetHeading = 100;

                public int TargetHeading {
                    get => _targetHeading;
                    set {
                        _targetHeading = value;
                        UpdateHeadingWindow();
                    }
                }

                [FieldChangeCallback(nameof(TargetAltitude))]
                public int _targetAltitude = 100;

                public int TargetAltitude {
                    get => _targetAltitude;
                    set {
                        _targetAltitude = value;
                        UpdateAltitudeWindow();
                    }
                }

                [FieldChangeCallback(nameof(IsVerticalSpeedManaged))]
                public bool _isVerticalSpeedManaged;

                public bool IsVerticalSpeedManaged {
                    get => _isVerticalSpeedManaged;
                    set {
                        _isVerticalSpeedManaged = value;
                        UpdateVerticalSpeedWindow();
                    }
                }

                [FieldChangeCallback(nameof(TargetVerticalSpeed))]
                public int _targetVerticalSpeed;

                public int TargetVerticalSpeed {
                    get => _targetVerticalSpeed;
                    set {
                        _targetVerticalSpeed = value;
                        UpdateVerticalSpeedWindow();
                    }
                }

                [FieldChangeCallback(nameof(TargetFPA))]
                public double _targetFPA;

                public double TargetFPA {
                    get => _targetFPA;
                    set {
                        _targetFPA = value;
                        UpdateVerticalSpeedWindow();
                    }
                }

            #endregion
        */

    }

    public enum FCUMode {
        HeadingVerticalSpeed,
        TrackFPA
    }

    public enum GuidanceMode {
        Managed,    // 管理模式 (推 - 由FMGC计算)
        Selected    // 选择模式 (拉 - 由FCU面板数值决定)
    }
    public enum APStatus {
        Off,
        AP1,
        AP2,
        AP12
    }
    public enum VerticalFlightMode {
        ALT_HOLD,   // 高度层保持
        ALT_STAR,
        CLB,        // 管理爬升
        DES,        // 管理下降
        OP_CLB,     // 开放爬升
        OP_DES,     // 开放下降
        VS,         // 垂直速度模式
        FPA,        // 飞行轨迹角模式
        GS,         // 进近下滑道捕获/跟踪
        EXPED,       // 紧急爬升/下降
        SRS,
        None        //无数据
    }
    public enum LateralFlightMode {
        HDG,        // 航向选择
        NAV,        // 航线管理导航
        LOC,        // 航向道捕获/跟踪
        LAND,        // 着陆模式
        RWY,
        RWY_TRK,
        GA_TRK,
        TRK,
        None        //无数据
    }
    public enum ApprModeStatus {
        Off,        // 未激活
        Armed,      // 已预置 (等待截获LOC/GS)
        Engaged     // 已捕获 (正在跟踪LOC/GS)
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(A320VAU.FCU.FCU))]
public class A320_FCUEditor : Editor {
    public override void OnInspectorGUI() {
        // 绘制默认面板属性
        DrawDefaultInspector();

        var fcu = (FCU)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.HelpBox("【FCU 模拟测试控制台】", MessageType.Info);
        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetSpeed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("speedGuidance"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetHeading"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isTrkFpaMode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lateralGuidance"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lateralMode"));

        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetAltitude"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetVS"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetFPA"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalGuidance"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalMode"));

        EditorGUILayout.Space(5);
        // --- SPEED SECTION ---
        EditorGUILayout.LabelField("1. Speed / Mach Controls", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("PUSH (Managed)")) fcu.PushSpeedKnob();
        if (GUILayout.Button("PULL (Selected)")) fcu.PullSpeedKnob();
        if (GUILayout.Button("SPD -10")) fcu.TurnSpeedKnob(-10f);
        if (GUILayout.Button("SPD +10")) fcu.TurnSpeedKnob(10f);
        if (GUILayout.Button("SPD/MACH")) fcu.ToggleSpdMach();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // --- LATERAL SECTION ---
        EditorGUILayout.LabelField("2. Lateral (HDG/NAV) Controls", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("PUSH (NAV)")) fcu.PushHeadingKnob();
        if (GUILayout.Button("PULL (HDG)")) fcu.PullHeadingKnob();
        if (GUILayout.Button("HDG -10")) fcu.TurnHeadingKnob(-10f);
        if (GUILayout.Button("HDG +10")) fcu.TurnHeadingKnob(10f);
        if (GUILayout.Button("HDG/TRK")) fcu.ToggleHdgTrkMode();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // --- ALTITUDE / VS SECTION ---
        EditorGUILayout.LabelField("3. Altitude & VS Controls", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("PUSH ALT")) fcu.PushAltitudeKnob();
        if (GUILayout.Button("PULL ALT")) fcu.PullAltitudeKnob();
        if (GUILayout.Button("ALT -1000")) fcu.TurnAltitudeKnob(-1f);
        if (GUILayout.Button("ALT +1000")) fcu.TurnAltitudeKnob(1f);
        if (GUILayout.Button("STEP 100/1000")) fcu.ToggleAltitudeStep();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("PUSH VS (Level Off)")) fcu.PushVSKnobToLevelOff();
        if (GUILayout.Button("PULL VS")) fcu.PullVSKnob();
        if (GUILayout.Button("VS -500")) fcu.TurnVSKnob(-5f);
        if (GUILayout.Button("VS +500")) fcu.TurnVSKnob(5f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // --- MODE BUTTONS SECTION ---
        EditorGUILayout.LabelField("4. Autopilot Push Buttons", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = fcu.isAP1Active ? Color.green : Color.white;
        if (GUILayout.Button("AP 1")) fcu.PressAP1();

        GUI.backgroundColor = fcu.isAP2Active ? Color.green : Color.white;
        if (GUILayout.Button("AP 2")) fcu.PressAP2();

        GUI.backgroundColor = fcu.isATHRActive ? Color.green : Color.white;
        if (GUILayout.Button("A/THR")) fcu.PressATHR();

        GUI.backgroundColor = fcu.locStatus != ApprModeStatus.Off ? Color.yellow : Color.white;
        if (GUILayout.Button("LOC")) fcu.PressLOC();

        GUI.backgroundColor = fcu.apprStatus != ApprModeStatus.Off ? Color.yellow : Color.white;
        if (GUILayout.Button("APPR")) fcu.PressAPPR();

        GUI.backgroundColor = fcu.isExpedActive ? Color.red : Color.white;
        if (GUILayout.Button("EXPED")) fcu.PressEXPED();

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // 强制重绘GUI刷新画面
        if (GUI.changed && Application.isPlaying) {
            fcu.UpdateFCUDisplay();
            EditorUtility.SetDirty(fcu);
        }
    }
}
#endif