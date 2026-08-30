using A320VAU.Common;
using A320VAU.FCU;
using A320VAU.Utils;
using Avionics.Systems.Common;
using UdonSharp;
using UnityEngine;
using YuxiFlightInstruments.BasicFlightData;

namespace A320VAU.FMGC {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class FMGCFlightPhase : UdonSharpBehaviour {
        public FMGC fmgc;
        public FCU.FCU fcu;

        private DependenciesInjector _injector;
        private AircraftSystemData _aircraftSystemData;
        private ADIRU.ADIRU _adirud;
        private SystemEventBus _eventBus;

        public KeyCode activeApproachKey = KeyCode.Alpha9;
        private bool _isKeyPass;

        public int accelerateAltitude = 1500;

        private bool _isLastFrameTouchDown;
        private float _touchDownAt = -1f;

        [FieldChangeCallback(nameof(CurrentFlightPhase))]
        private FlightPhase _currentFlightPhase = FlightPhase.PreFlight;
        public FlightPhase CurrentFlightPhase {
            get => _currentFlightPhase;
            set {
                FlightPhase previousPhase = _currentFlightPhase;
                _currentFlightPhase = value;

                OnFlightPhaseChanged(previousPhase, value);
                LogCurrentFlightPhase();
                _eventBus.SendEvent("FlightPhaseChanged");
            }
        }

        private readonly float UPDATE_INTERVAL = UpdateIntervalUtil.GetUpdateIntervalFromSeconds(1);
        private float _lastUpdate = -1f;

        private void Start() {
            _injector = DependenciesInjector.GetInstance(this);
            _aircraftSystemData = _injector.equipmentData;
            _adirud = _injector.adiru;
            _eventBus = _injector.systemEventBus;
        }

        private void LateUpdate() {
            if (Input.GetKey(activeApproachKey)) {
                if (!_isKeyPass) {
                    _isKeyPass = true;
                    if (!(CurrentFlightPhase == FlightPhase.PreFlight || CurrentFlightPhase == FlightPhase.Done)) {
                        CurrentFlightPhase = FlightPhase.Approach;
                    }
                }
            }
            else {
                _isKeyPass = false;
            }

            if (!UpdateIntervalUtil.CanUpdate(ref _lastUpdate, UPDATE_INTERVAL))
                return;

            ShouldGoToNextPhase();
        }

        private void OnFlightPhaseChanged(FlightPhase fromPhase, FlightPhase toPhase) {
            if (fcu == null) return;
            if (toPhase == FlightPhase.PreFlight) { 
                fcu.verticalMode = FCU.VerticalFlightMode.None;
                fcu.lateralMode = FCU.LateralFlightMode.None;
            }

            // 1. 进入 Takeoff 阶段：推油门起飞时写入 SRS 与 RWY
            if (toPhase == FlightPhase.Takeoff) {
                fcu.verticalMode = VerticalFlightMode.SRS;
                fcu.verticalGuidance = GuidanceMode.Managed;

                fcu.lateralMode = LateralFlightMode.RWY;
                fcu.lateralGuidance = GuidanceMode.Managed;
            }

            // 2. 达到加速高度进入 Climb 阶段：SRS 退场，自动切入 CLB 与 NAV
            if (toPhase == FlightPhase.Climb) {
                if (fcu.verticalMode == VerticalFlightMode.SRS) {
                    fcu.verticalMode = (fcu.verticalGuidance == GuidanceMode.Managed)
                        ? VerticalFlightMode.CLB
                        : VerticalFlightMode.OP_CLB;
                }

                if (fcu.lateralMode == LateralFlightMode.RWY_TRK || fcu.lateralMode == LateralFlightMode.RWY) {
                    fcu.lateralMode = (fcu.lateralGuidance == GuidanceMode.Managed)
                        ? LateralFlightMode.NAV
                        : LateralFlightMode.HDG;
                }
            }
        }

        private void LogCurrentFlightPhase() {
            switch (CurrentFlightPhase) {
                case FlightPhase.PreFlight:
                    Debug.Log("FlightPhase: PreFlight");
                    break;
                case FlightPhase.Takeoff:
                    Debug.Log("FlightPhase: Takeoff");
                    break;
                case FlightPhase.Climb:
                    Debug.Log("FlightPhase: Climb");
                    break;
                case FlightPhase.Cruise:
                    Debug.Log("FlightPhase: Cruise");
                    break;
                case FlightPhase.Descent:
                    Debug.Log("FlightPhase: Descent");
                    break;
                case FlightPhase.Approach:
                    Debug.Log("FlightPhase: Approach");
                    break;
                case FlightPhase.GoAround:
                    Debug.Log("FlightPhase: GoAround");
                    break;
                case FlightPhase.Done:
                    Debug.Log("FlightPhase: Done");
                    break;
            }
        }

        private void ShouldGoToNextPhase() {
            switch (CurrentFlightPhase) {
                case FlightPhase.PreFlight:
                    if (Mathf.Approximately(_aircraftSystemData.engine1ThrottleLeveler, 1f) ||
                        Mathf.Approximately(_aircraftSystemData.engine2ThrottleLeveler, 1f)) {
                        CurrentFlightPhase = FlightPhase.Takeoff;
                    }
                    break;
                case FlightPhase.Takeoff:
                    // 动态检测离地：主起落架离开地面后，跑道模式由 RWY 自动转为 RWY TRK
                    if (fcu != null && fcu.lateralMode == LateralFlightMode.RWY) {
                        if (!_injector.equipmentData.isAircraftGrounded) {
                            fcu.lateralMode = LateralFlightMode.RWY_TRK;
                            fcu.targetHeading = _adirud.irs.heading;
                        }
                    }

                    if (_adirud.adr.pressureAltitude >= accelerateAltitude) {
                        CurrentFlightPhase = FlightPhase.Climb;
                    }
                    break;

                    break;
                case FlightPhase.Climb:
                    if (Mathf.Approximately(_adirud.adr.pressureAltitude, fmgc.flightPlan.cruiseAltitude) ||
                        _adirud.adr.pressureAltitude >= fmgc.flightPlan.cruiseAltitude) {
                        CurrentFlightPhase = FlightPhase.Cruise;
                    }

                    break;
                case FlightPhase.Cruise:
                    // We don't have managed descent or selected descent for now
                    if (_adirud.adr.pressureAltitude < fmgc.flightPlan.cruiseAltitude - 100f &&
                        _adirud.adr.verticalSpeed < -500f) {
                        CurrentFlightPhase = FlightPhase.Descent;
                    }
                    break;
                case FlightPhase.Descent:
                    // if (fmgc.flightPlan.arrivalAirportIndex != -1) {
                    //     var arrivalAirportTransform = fmgc.navaidDatabase.waypointTransforms[fmgc.flightPlan.arrivalAirportIndex];
                    //     var arrivalAirportPosition = arrivalAirportTransform.position;
                    //     arrivalAirportPosition.y = 0;
                    //
                    //     var aircraftPosition = transform.position;
                    //     aircraftPosition.y = 0;
                    // }
                    break;
                case FlightPhase.Approach:
                    if (_injector.saccAirVehicle.Taxiing) {
                        if (!_isLastFrameTouchDown) {
                            _isLastFrameTouchDown = true;
                            _touchDownAt = Time.time;
                        }

                        if (Time.time - _touchDownAt > 30f) {
                            CurrentFlightPhase = FlightPhase.Done;
                            return;
                        }
                    }
                    else {
                        _isLastFrameTouchDown = false;
                        _touchDownAt = -1f;
                    }

                    if (Mathf.Approximately(_aircraftSystemData.engine2ThrottleLeveler, 1f) ||
                        Mathf.Approximately(_aircraftSystemData.engine1ThrottleLeveler, 1f)) {
                        CurrentFlightPhase = FlightPhase.GoAround;
                    }
                    break;
                case FlightPhase.GoAround:
                    break;
                case FlightPhase.Done:
                    CurrentFlightPhase = FlightPhase.PreFlight;
                    break;
            }
        }
    }

    public enum FlightPhase {
        PreFlight,
        Takeoff,
        Climb,
        Cruise,
        Descent,
        Approach,
        GoAround,
        Done
    }
}