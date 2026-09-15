using A320VAU.Common;
using UdonSharp;
using YuxiFlightInstruments.BasicFlightData;
using A320VAU.AtmosphereModel;
using UnityEngine;

namespace A320VAU.ADIRU {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [DefaultExecutionOrder(2015)] //第一个启动的电器
    public class ADR : UdonSharpBehaviour {
        public YFI_FlightDataInterface flightDataInterface;
        public EarthAtmosphereModel airDataModule;

        //一次参数从_flightDataInterface里面取
        //二次参数从airDataModule里面取
        public float pressureAltitude => flightDataInterface.altitude;
        public float trueAirSpeed => flightDataInterface.TAS;
        public float instrumentAirSpeed => flightDataInterface.TAS;
        public float mach => airDataModule.MachNumber;
        public float angleOfAttack => flightDataInterface.angleOfAttack;
        public float AOAPitch => flightDataInterface.AOAPitch;
        public float verticalSpeed => flightDataInterface.verticalSpeed;
        public float verticalG => flightDataInterface.verticalG;
        public float TemperatureTotal => airDataModule.TemperatureTotal;
        public float Vstall_1g => flightDataInterface.velocityStall1G;
        public float Vstall => flightDataInterface.velocityStall;
        private void Start() {

        }

    }
}