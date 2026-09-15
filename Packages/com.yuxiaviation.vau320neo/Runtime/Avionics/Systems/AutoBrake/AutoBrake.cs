using A320VAU.Brake;
using A320VAU.Common;
using Avionics.Systems.Common;
using JetBrains.Annotations;
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;

namespace A320VAU {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class AutoBrake : UdonSharpBehaviour {
        public Animator indicatorAnimator;

        private DependenciesInjector _dependenciesInjector;
        private SaccAirVehicle _saccAirVehicle;
        private AircraftSystemData _aircraftSystemData;
        private ADIRU.ADIRU _adiru;

        public DFUNC_a320_Brake brake;

        private Vector3 _lastVelocity;

    #region PID

        private float _previousError;
        private float _integral;

        public float Kp = 0.01f;
        public float Ki = 0.01f;
        public float Kd = 0.01f;

    #endregion

        private bool _isLastFrameAircraftTouchdown;

        public bool isAutoBrakeActive {
            get => _isAutoBrakeActive;
            private set {
                if (_isAutoBrakeActive == value) return;

                Reset();
                _isAutoBrakeActive = value;

                RequestSerialization();
            }
        }

        [UdonSynced] [SerializeField] [HideInInspector]
        private bool _isAutoBrakeActive;

    #region Animation

        private readonly int AUTO_BRK_MODE = Animator.StringToHash("AutoBrkMode");
        private readonly int DECELERATION_HASH = Animator.StringToHash("Deceleration");

    #endregion

        public AutoBrakeMode currentAutoBrakeMode {
            get => _currentAutoBrakeMode;
            private set {
                if (_currentAutoBrakeMode == value) return;

                _currentAutoBrakeMode = value;
                RequestSerialization();
            }
        }

        [UdonSynced] [SerializeField] [HideInInspector]
        private AutoBrakeMode _currentAutoBrakeMode = AutoBrakeMode.None;

        private const float _lowBrakeDecelerationRate = -1.7f; // -1.7m/s²
        private const float _medBrakeDecelerationRate = -3f; // -3m/s²

        private void Start() {
            _dependenciesInjector = DependenciesInjector.GetInstance(this);
            _saccAirVehicle = _dependenciesInjector.saccAirVehicle;
            _aircraftSystemData = _dependenciesInjector.equipmentData;
            _adiru = _dependenciesInjector.adiru;

            _dependenciesInjector.systemEventBus.RegisterSaccEvent(this);
        }

        public void SFEXT_O_RespawnButton() {
            isAutoBrakeActive = false;
            currentAutoBrakeMode = AutoBrakeMode.None;
        }

    #region Touch Switch Event

        [PublicAPI]
        public void SelectAutoBrakeLow() {
            if (currentAutoBrakeMode != AutoBrakeMode.Low) {
                currentAutoBrakeMode = AutoBrakeMode.Low;
                return;
            }

            currentAutoBrakeMode = AutoBrakeMode.None;
        }

        [PublicAPI]
        public void SelectAutoBrakeMed() {
            if (currentAutoBrakeMode != AutoBrakeMode.Med) {
                currentAutoBrakeMode = AutoBrakeMode.Med;
                return;
            }

            currentAutoBrakeMode = AutoBrakeMode.None;
        }

        [PublicAPI]
        public void SelectedAutoBrakeMax() {
            if (!_aircraftSystemData.isAircraftGrounded) {
                currentAutoBrakeMode = AutoBrakeMode.None;
                return;
            }

            if (currentAutoBrakeMode != AutoBrakeMode.Max) {
                currentAutoBrakeMode = AutoBrakeMode.Max;
                return;
            }

            currentAutoBrakeMode = AutoBrakeMode.None;
        }

    #endregion

    #region Update

        private void LateUpdate() {
            var decelerationRate = GetDecelerationRate();

            if (currentAutoBrakeMode == AutoBrakeMode.Max && !_aircraftSystemData.isAircraftGrounded) {
                currentAutoBrakeMode = AutoBrakeMode.None;
            }

            if (_aircraftSystemData.isOwner) {
                UpdateAutoBrakeActive();
                UpdateAutoBrake(decelerationRate);
            }

            UpdateIndicator(decelerationRate);

            _isLastFrameAircraftTouchdown = _aircraftSystemData.isAircraftGrounded;
        }


        private void UpdateAutoBrake(float decelerationRate) {
            if (isAutoBrakeActive) {
                float brakeInput;
                if (currentAutoBrakeMode == AutoBrakeMode.Max) {
                    brakeInput = 1f;
                }
                else {
                    var targetDecelerationRate = GetTargetDecelerationRate();

                    var error = decelerationRate - targetDecelerationRate;
                    _integral += error * Time.deltaTime;
                    var derivative = (error - _previousError) / Time.deltaTime;
                    brakeInput = Kp * error + Ki * _integral + Kd * derivative;

                    _previousError = error;

                    Debug.Log($"{decelerationRate} | {error} | {brake.autoBrakeInput}");
                }

                brake.autoBrakeInput = Mathf.Clamp(brakeInput, 0f, 1f);
            }
            else {
                brake.autoBrakeInput = 0;
            }
        }

        private void UpdateAutoBrakeActive() {
            if (isAutoBrakeActive && brake.isManuelBrakeInUse) {
                isAutoBrakeActive = false;
                currentAutoBrakeMode = AutoBrakeMode.None;
                return;
            }

            if (currentAutoBrakeMode == AutoBrakeMode.None) {
                isAutoBrakeActive = false;
                return;
            }

            if (!_aircraftSystemData.isAircraftGrounded) {
                isAutoBrakeActive = false;
                return;
            }

            if (isAutoBrakeActive) return;

            switch (currentAutoBrakeMode) {
                case AutoBrakeMode.Low:
                    if (!_isLastFrameAircraftTouchdown)
                        isAutoBrakeActive = true;
                    break;
                case AutoBrakeMode.Med:
                    if (!_isLastFrameAircraftTouchdown)
                        isAutoBrakeActive = true;
                    break;
                case AutoBrakeMode.Max:
                    isAutoBrakeActive = _aircraftSystemData.isBothThrottleLevelerIdle && _adiru.irs.groundSpeed >= 72;
                    break;
                default:
                    isAutoBrakeActive = false;
                    break;
            }
        }

        private void UpdateIndicator(float decelerationRate) {
            float animationValue;
            switch (currentAutoBrakeMode) {
                case AutoBrakeMode.Low:
                    animationValue = 0f;
                    break;
                case AutoBrakeMode.Med:
                    animationValue = 1f;
                    break;
                case AutoBrakeMode.Max:
                    animationValue = 2f;
                    break;
                case AutoBrakeMode.None:
                default:
                    animationValue = 3f;
                    break;
            }

            indicatorAnimator.SetFloat(AUTO_BRK_MODE, animationValue / 3f);

            if (!isAutoBrakeActive) {
                indicatorAnimator.SetBool(DECELERATION_HASH, false);
                return;
            }

            var isReachDecelerationRateTarget = IsReachDecelerationRateTarget(decelerationRate);

            indicatorAnimator.SetBool(DECELERATION_HASH, isReachDecelerationRateTarget);
        }

    #endregion

        private float GetDecelerationRate() {
            var velocity = _adiru.irs.velocity;

            var acceleration = (velocity - _lastVelocity) / Time.fixedDeltaTime;
            var temp = _saccAirVehicle.transform.rotation * acceleration;

            _lastVelocity = velocity;

            return temp.z;
        }

        private float GetTargetDecelerationRate() {
            switch (currentAutoBrakeMode) {
                case AutoBrakeMode.Low:
                    return _lowBrakeDecelerationRate;
                case AutoBrakeMode.Med:
                    return _medBrakeDecelerationRate;
                case AutoBrakeMode.None:
                    return 0f;
                default:
                    return -3f;
            }
        }

        private bool IsReachDecelerationRateTarget(float decelerationRate) {
            if (currentAutoBrakeMode == AutoBrakeMode.Max) return true;

            var targetDecelerationRate = GetTargetDecelerationRate() * 0.8f;
            return decelerationRate < targetDecelerationRate;
        }

        public void Reset() {
            _previousError = 0f;
            _integral = 0f;
        }
    }

    public enum AutoBrakeMode {
        Low,
        Med,
        Max,
        None
    }
}