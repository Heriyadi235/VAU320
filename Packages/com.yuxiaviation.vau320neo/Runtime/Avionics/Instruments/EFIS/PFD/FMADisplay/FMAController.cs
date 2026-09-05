using System;
using A320VAU.Common;
using A320VAU.Utils;
using Avionics.Systems.Common;
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace A320VAU.PFD {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FMAController : UdonSharpBehaviour {
        private AircraftSystemData _aircraftSystemData;
        private DFUNC_AltHold _altHoldDFunc;
        private DFUNC_a320_AutoThrust _cruiseDFunc;
        private AutoBrake _autoBrake;
        private DependenciesInjector _injector;
        
        private readonly float UPDATE_INTERVAL = UpdateIntervalUtil.GetUpdateIntervalFromFPS(30);
        private float _lastUpdate;

        private void Start() {
            _injector = DependenciesInjector.GetInstance(this);

            _aircraftSystemData = _injector.equipmentData;
            _cruiseDFunc = _injector.autoThrust;
            _altHoldDFunc = _injector.altHold;
            _autoBrake = _injector.autoBrake;

            AutoThrustModeText.text = "";
            ManThrText.text = "";
            AutoBrkArmModeText.text = "";
            ManThrRoot.SetActive(false);

            VerticalArmModeText.text = "";
            VerticalModeText.text = "";

            HorizontalArmModeText.text = "";
            HorizontalModeText.text = "";

            ApproachAbilityText.text = "";
            ApproachMinimumText.text = "";

            AutoPilotText.text = "";
            FlightDirectorText.text = "";
            AutoThrustGameObject.SetActive(false);

            CommonModeRoot.SetActive(false);
            CommonModeText.text = "";

            SpecialMessageRoot.SetActive(false);
            MessageText.text = "";
        }

        public void LateUpdate() {
            if (!UpdateIntervalUtil.CanUpdate(ref _lastUpdate, UPDATE_INTERVAL)) return;

            switch (_aircraftSystemData.throttleLevelerSlot) {
                case ThrottleLevelerSlot.TOGA:
                    ManThrType = ManThrType.TOGA;
                    break;
                case ThrottleLevelerSlot.FlexMct:
                    ManThrType = ManThrType.MCT;
                    break;
                default:
                    ManThrType = ManThrType.None;
                    break;
            }

            /*
            if (_altHoldDFunc.AltHold) {
                VerticalActiveMode = "ALT";
                LateralActiveMode = "HDG";
                IsAutoPilot1Active = true;
            }
            else {
                VerticalActiveMode = "";
                LateralActiveMode = "";
                IsAutoPilot1Active = false;
            }
            */
            

            switch (_autoBrake.currentAutoBrakeMode) {
                case A320VAU.AutoBrakeMode.Low:
                    IsAutoBrakeArm = true;
                    AutoBrakeMode = AutoBrakeMode.LOW;
                    break;
                case A320VAU.AutoBrakeMode.Med:
                    IsAutoBrakeArm = true;
                    AutoBrakeMode = AutoBrakeMode.MED;
                    break;
                case A320VAU.AutoBrakeMode.Max:
                    IsAutoBrakeArm = true;
                    AutoBrakeMode = AutoBrakeMode.MAX;
                    break;
                case A320VAU.AutoBrakeMode.None:
                    IsAutoBrakeArm = false;
                    AutoBrakeMode = AutoBrakeMode.None;
                    break;
                default:
                    IsAutoBrakeArm = false;
                    AutoBrakeMode = AutoBrakeMode.None;
                    break;
            }

            IsAutoBrakeActive = _autoBrake.isAutoBrakeActive;

            if (_autoBrake.isAutoBrakeActive) {

            }
        }

        private void UpdateAutoThrustDisplay() {
            ManThrRoot.SetActive((_isAutoThrustArm || !_isAutoThrustActive) && ManThrType != ManThrType.None);
            AutoBrkArmModeGameObject.SetActive(!_isAutoBrakeActive);

            var autoBrakeString = "";
            switch (AutoBrakeMode) {
                case AutoBrakeMode.MAX:
                    autoBrakeString = "MAX";
                    break;
                case AutoBrakeMode.MED:
                    autoBrakeString = "MED";
                    break;
                case AutoBrakeMode.LOW:
                    autoBrakeString = "LOW";
                    break;
            }

            AutoThrustModeText.text = IsAutoBrakeActive ? $"BRK {autoBrakeString}" : AutoThrustMode;
            AutoBrkArmModeText.text = IsAutoBrakeArm ? $"BRK {autoBrakeString}" : "";

            switch (ManThrType) {
                case ManThrType.TOGA:
                    ManThrText.text = "MAN\nTOGA";
                    break;
                case ManThrType.MCT:
                    ManThrText.text = "MAN\nMCT";
                    break;
                case ManThrType.FLEX:
                    ManThrText.text = "MAN\nFLX+";
                    break;
                case ManThrType.None:
                    ManThrText.text = "";
                    break;
            }
        }

        private void UpdateLateralModeDisplay() {
            UpdateCommonModeDisplay();
            HorizontalModeText.text = LateralActiveMode;
            HorizontalArmModeText.text = LateralArmMode;
        }

        private void UpdateVerticalModeDisplay() {
            UpdateCommonModeDisplay();
            VerticalModeText.text = VerticalActiveMode;
            VerticalArmModeText.text = VerticalArmMode;
        }

        private void UpdateApproachDisplay() {
            var approachAbilityString = "";
            switch (ApproachAbility) {
                case ApproachAbility.Cat1:
                    approachAbilityString += "CAT 1";
                    break;
                case ApproachAbility.Cat2:
                    approachAbilityString += "CAT 2";
                    break;
                case ApproachAbility.Cat3:
                    approachAbilityString += "CAT 3";
                    break;
            }

            if (ApproachMinimumType != ApproachMinimumType.None && ApproachMinimumHeight > 0) {
                var approachMinimumTypeString = "";
                switch (ApproachMinimumType) {
                    case ApproachMinimumType.BARO:
                        approachMinimumTypeString = "BARO";
                        break;
                    case ApproachMinimumType.RADIO:
                        approachMinimumTypeString = "RADIO";
                        break;
                }

                ApproachMinimumText.text =
                    $"{approachMinimumTypeString} <color=#38FFFE>{ApproachMinimumHeight}</color>";
            }

            if (ApproachAbility != ApproachAbility.None && (IsAutoPilot1Active || IsAutoPilot2Active)) {
                var channels = "\nSINGLE";
                if (IsAutoPilot1Active && IsAutoPilot2Active)
                    channels = "\nDUAL";

                approachAbilityString += channels;
            }

            ApproachAbilityText.text = approachAbilityString;
        }

        private void UpdateAutopilotStatusDisplay() {
            AutoThrustGameObject.SetActive(_isAutoThrustActive);
            AutoThrustText.text = IsAutoThrustArm ? $"<color={AirbusAvionicsTheme.Blue}>A/THR</color>" : "A/THR";

            AutoPilotText.text = "";
            if (IsAutoPilot1Active && IsAutoPilot2Active) {
                AutoPilotText.text = "AP1+2";
                return;
            }

            if (IsAutoPilot1Active)
                AutoPilotText.text = "AP1";
            if (IsAutoPilot2Active)
                AutoPilotText.text = "AP2";

            FlightDirectorText.text = "";
            if (IsFlightDirector1Active && IsFlightDirector2Active) {
                FlightDirectorText.text = "1 FD 2";
                return;
            }

            if (IsFlightDirector1Active)
                FlightDirectorText.text = "1 FD -";
            if (IsFlightDirector2Active)
                FlightDirectorText.text = "- FD 2";
        }

        private void UpdateSpecialMessageDisplay() {
            SpecialMessageRoot.SetActive(!string.IsNullOrWhiteSpace(SpecialMessage));
            MessageText.text = SpecialMessage;

            if (!string.IsNullOrWhiteSpace(SpecialMessageColor))
                MessageText.text = $"<color={SpecialMessageRoot}>{SpecialMessage}</color>";
        }

        private void UpdateCommonModeDisplay() {
            if (!string.IsNullOrWhiteSpace(LateralActiveMode) && !string.IsNullOrWhiteSpace(VerticalActiveMode)) {
                CommonModeRoot.SetActive(LateralActiveMode == VerticalActiveMode);
                CommonModeText.text = LateralActiveMode;
            }
        }

    #region UI Control

    #region Autothrust Mode

        public Text AutoThrustModeText;
        public GameObject ManThrRoot;
        public Text ManThrText;
        public GameObject AutoBrkArmModeGameObject;
        public Text AutoBrkArmModeText;

    #endregion

    #region Vertical Mode

        public Text VerticalModeText;
        public Text VerticalArmModeText;

    #endregion

    #region Lateral Mode

        public Text HorizontalModeText;
        public Text HorizontalArmModeText;

    #endregion

    #region Approach

        public Text ApproachAbilityText;
        public Text ApproachMinimumText;

    #endregion

    #region Autopilot Status

        public Text AutoPilotText;
        public Text FlightDirectorText;
        public GameObject AutoThrustGameObject;
        public Text AutoThrustText;

    #endregion

    #region Common Mode

        public GameObject CommonModeRoot;
        public Text CommonModeText;

    #endregion

    #region Special Message

        public GameObject SpecialMessageRoot;
        public Text MessageText;

    #endregion

    #endregion

    #region Property

    #region AutoThrust

        [FieldChangeCallback(nameof(IsAutoThrustArm))]
        public bool _isAutoThrustArm;

        public bool IsAutoThrustArm {
            get => _isAutoThrustArm;
            set {
                _isAutoThrustArm = value;
                UpdateAutoThrustDisplay();
                UpdateAutopilotStatusDisplay();
            }
        }

        [FieldChangeCallback(nameof(AutoThrustMode))]
        public string _autoThrustMode = "";

        public string AutoThrustMode {
            get => _autoThrustMode;
            set {
                _autoThrustMode = value;
                UpdateAutoThrustDisplay();
            }
        }

        [FieldChangeCallback(nameof(IsAutoThrustActive))]
        public bool _isAutoThrustActive;

        public bool IsAutoThrustActive {
            get => _isAutoThrustActive;
            set {
                _isAutoThrustActive = value;
                UpdateAutoThrustDisplay();
                UpdateAutopilotStatusDisplay();
            }
        }

        [FieldChangeCallback(nameof(ManThrType))]
        public ManThrType _manThrType = ManThrType.None;

        public ManThrType ManThrType {
            get => _manThrType;
            set {
                _manThrType = value;
                UpdateAutoThrustDisplay();
            }
        }

        [FieldChangeCallback(nameof(AutoBrakeMode))]
        public AutoBrakeMode _autoBrakeMode = AutoBrakeMode.None;

        public AutoBrakeMode AutoBrakeMode {
            get => _autoBrakeMode;
            set {
                _autoBrakeMode = value;
                UpdateAutoThrustDisplay();
            }
        }

        [FieldChangeCallback(nameof(IsAutoBrakeActive))]
        public bool _isAutoBrakeActive;

        public bool IsAutoBrakeActive {
            get => _isAutoBrakeActive;
            set {
                _isAutoBrakeActive = value;
                UpdateAutoThrustDisplay();
            }
        }

        [FieldChangeCallback(nameof(IsAutoBrakeArm))]
        public bool _isAutoBrakeArm;

        public bool IsAutoBrakeArm {
            get => _isAutoBrakeArm;
            set {
                _isAutoBrakeArm = value;
                UpdateAutoThrustDisplay();
            }
        }

    #endregion

    #region Vertical Mode

        [FieldChangeCallback(nameof(VerticalActiveMode))]
        public string _verticalActiveMode = "";

        public string VerticalActiveMode {
            get => _verticalActiveMode;
            set {
                _verticalActiveMode = value;
                UpdateVerticalModeDisplay();
            }
        }

        [FieldChangeCallback(nameof(VerticalArmMode))]
        public string _verticalArmMode = "";

        public string VerticalArmMode {
            get => _verticalArmMode;
            set {
                _verticalArmMode = value;
                UpdateVerticalModeDisplay();
            }
        }

    #endregion

    #region Lateral Mode

        [FieldChangeCallback(nameof(LateralActiveMode))]
        public string _lateralActiveMode = "";

        public string LateralActiveMode {
            get => _lateralActiveMode;
            set {
                _lateralActiveMode = value;
                UpdateLateralModeDisplay();
            }
        }

        [FieldChangeCallback(nameof(LateralArmMode))]
        public string _lateralArmMode = "";

        public string LateralArmMode {
            get => _lateralArmMode;
            set {
                _lateralArmMode = value;
                UpdateLateralModeDisplay();
            }
        }

    #endregion

    #region Approach

        [FieldChangeCallback(nameof(ApproachAbility))]
        public ApproachAbility _approachAbility = ApproachAbility.None;

        public ApproachAbility ApproachAbility {
            get => _approachAbility;
            set {
                _approachAbility = value;
                UpdateApproachDisplay();
            }
        }

        [FieldChangeCallback(nameof(ApproachMinimumType))]
        public ApproachMinimumType _approachMinimumType = ApproachMinimumType.None;

        public ApproachMinimumType ApproachMinimumType {
            get => _approachMinimumType;
            set {
                _approachMinimumType = value;
                UpdateApproachDisplay();
            }
        }

        [FieldChangeCallback(nameof(ApproachMinimumHeight))]
        public int _approachMinimumHeight;

        public int ApproachMinimumHeight {
            get => _approachMinimumHeight;
            set {
                _approachMinimumHeight = value;
                UpdateApproachDisplay();
            }
        }

    #endregion

    #region Autopilot Status

    #region Autopilot

        [FieldChangeCallback(nameof(IsAutoPilot1Active))]
        public bool _isAutoPilot1Active;

        public bool IsAutoPilot1Active {
            get => _isAutoPilot1Active;
            set {
                _isAutoPilot1Active = value;
                UpdateAutopilotStatusDisplay();
            }
        }


        [FieldChangeCallback(nameof(IsAutoPilot2Active))]
        public bool _isAutoPilot2Active;

        public bool IsAutoPilot2Active {
            get => _isAutoPilot2Active;
            set {
                _isAutoPilot2Active = value;
                UpdateAutopilotStatusDisplay();
            }
        }

    #endregion

    #region FlightDirector

        [FieldChangeCallback(nameof(IsFlightDirector1Active))]
        public bool _isFlightDirector1Active;

        public bool IsFlightDirector1Active {
            get => _isFlightDirector1Active;
            set {
                _isFlightDirector1Active = value;
                UpdateAutopilotStatusDisplay();
            }
        }

        [FieldChangeCallback(nameof(IsFlightDirector2Active))]
        public bool _isFlightDirector2Active;

        public bool IsFlightDirector2Active {
            get => _isFlightDirector2Active;
            set {
                _isFlightDirector2Active = value;
                UpdateAutopilotStatusDisplay();
            }
        }

    #endregion

    #endregion

    #region Special Message

        [FieldChangeCallback(nameof(SpecialMessage))]
        public string _specialMessage = "";

        public string SpecialMessage {
            get => _specialMessage;
            set {
                _specialMessage = value;
                UpdateSpecialMessageDisplay();
            }
        }

        [FieldChangeCallback(nameof(SpecialMessageColor))]
        public string _specialMessageColor = "";

        public string SpecialMessageColor {
            get => _specialMessageColor;
            set {
                _specialMessageColor = value;
                UpdateSpecialMessageDisplay();
            }
        }

    #endregion

    #endregion
    }

    public enum ManThrType {
        TOGA = 0,
        FLEX = 1,
        MCT = 2,
        None = 3
    }

    public enum AutoBrakeMode {
        MAX = 0,
        MED = 1,
        LOW = 3,
        None = 4
    }

    public enum ApproachAbility {
        Cat1,
        Cat2,
        Cat3,
        None
    }

    public enum ApproachMinimumType {
        BARO,
        RADIO,
        None
    }
}