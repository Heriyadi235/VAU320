using A320VAU.Common;
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using YuxiFlightInstruments.BasicFlightData;

namespace A320VAU.ADIRU {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [DefaultExecutionOrder(2020)]// after ADR
    public class IRS : UdonSharpBehaviour {
        private DependenciesInjector _injector;
        private YFI_FlightDataInterface _flightDataInterface;
        private SaccAirVehicle _saccAirVehicle;

        public float pitch =>  _flightDataInterface.pitch ;
        public float bank => _flightDataInterface.bank ;
        public float trackPitchAngle => _flightDataInterface.trackPitchAngle ;
        public float trackSlipAngle => _flightDataInterface.SlipAngle ;
        public float heading => _flightDataInterface.heading ;
        public float track => heading;
        public float groundSpeed => _flightDataInterface.groundSpeed ;
        public Vector2 position =>  _saccAirVehicle.CenterOfMass.position;
        public Vector3 velocity => _flightDataInterface.currentVelocity;

        private void Awake() {
            Initialize();
        }

        private void Start() {
            Initialize();
        }

        private void Initialize() {
            _injector = DependenciesInjector.GetInstance(this);
            if (_injector == null) return;

            _flightDataInterface = _injector.flightData;
            _saccAirVehicle = _injector.saccAirVehicle;
        }
    }
}