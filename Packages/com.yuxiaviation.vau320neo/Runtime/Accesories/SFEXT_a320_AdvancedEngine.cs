using System;
using A320VAU.Common;
using Avionics.Systems.Common;
using EsnyaSFAddons.SFEXT;
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using A320VAU.AtmosphereModel;
using Random = UnityEngine.Random;
using YuxiFlightInstruments;
using YuxiFlightInstruments.BasicFlightData;
using static UnityEngine.EventSystems.EventTrigger;
//note:this script is original from https://github.com/esnya/EsnyaSFAddons
//to satisfy vau320's demand, moditied startup charcrastic and add force point

namespace A320VAU.SFEXT {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    [DefaultExecutionOrder(1000)] // After SoundController
    public class SFEXT_a320_AdvancedEngine : UdonSharpBehaviour {
        [Header("Misc")]
        public EarthAtmosphereModel gasProperty;
        public float externalTempurature => gasProperty.TemperatureStatic;
        public YFI_FlightDataInterface basicFilghtData;
        
        public AircraftSystemData aircraftSystemData;
        public float randomRange = 0.2f;
        //public float wheelWakeUpTorque = 1.0e-36f;

        public bool isAutoThrustActive;
        public float autoThrustInput;

        

        public AudioSource throttleLevelerAudioSource;
        public AudioClip togaClip;
        public AudioClip togaReleaseClip;
        public AudioClip mctFlexClip;
        public AudioClip clbClip;
        public AudioClip idleClip;
        public AudioClip revOnClip;
        public AudioClip revUnlockClip;
        public AudioClip revOffClip;
        public AudioClip revIdleClip;
        public AudioClip revMaxClip;

        private ThrottleLevelerSlot _lastThrottleLevelerSlot;

    #region SFEXT Core

        private bool initialized, isOwner, isPilot, hasPilot, isPassenger;
        public SaccAirVehicle airVehicle;
        private SaccEntity entity;


        public void SFEXT_L_EntityStart() {

            entity = basicFilghtData.entityControl;
            airVehicle = basicFilghtData.SAVControl;
            vehicleRigidbody = airVehicle.VehicleRigidbody;
            vehicleAnimator = airVehicle.VehicleAnimator;
            airVehicle.ThrottleStrength = 0;
            airVehicle.AccelerationResponse = 0;

            Power_Start();
            Sound_Start(entity);
            Fault_Start();
            Effect_Start();
            JetBrust_Start();

            gameObject.SetActive(false);
            if (forcePosition == null)
                forcePosition = gameObject;
            initialized = true;
        }

        public void SFEXT_G_PilotEnter() {
            hasPilot = true;
            gameObject.SetActive(true);
        }

        public void SFEXT_G_PilotExit() {
            hasPilot = false;
        }

        public void SFEXT_O_PilotEnter() {
            isOwner = isPilot = true;
        }

        public void SFEXT_O_PilotExit() {
            isPilot = false;
        }

        public void SFEXT_P_PassengerEnter() {
            isPassenger = true;
        }

        public void SFEXT_P_PassengerExit() {
            isPassenger = false;
        }

        public void SFEXT_O_TakeOwnership() {
            isOwner = true;
            gameObject.SetActive(true);//take controll 时候避免熄火
        }

        public void SFEXT_O_LoseOwnership() {
            isOwner = false;
        }

        public void SFEXT_G_Explode() {
            ResetStatus();
        }

        public void SFEXT_O_Explode() {
            ResetStatus();
        }

        public void SFEXT_G_RespawnButton() {
            ResetStatus();
        }

        public void SFEXT_O_RespawnButton() {
            ResetStatus();
        }

        public void SFEXT_L_BoardingEnter() {
            onBoarding = true;
        }

        public void SFEXT_L_BoardingExit() {
            onBoarding = false;
        }

        public void SFEXT_G_TouchDownWater() {
            if (isOwner) {
                stall = true;
                broken = true;
            }
        }

        //private WheelCollider[] wheels;

        private void OnEnable() {
            //if (wheels == null) wheels = GetComponentInParent<Rigidbody>().GetComponentsInChildren<WheelCollider>(true);
            //foreach (var wheel in wheels) wheel.motorTorque += wheelWakeUpTorque;
        }

        private void OnDisable() {
            //foreach (var wheel in wheels) wheel.motorTorque -= wheelWakeUpTorque;
        }

        private void FixedUpdate() {
            if (!initialized) return;
            if (isOwner) Power_OwnerFixedUpdate();
        }

        private void Update() {
            if (!initialized) return;

            var deltaTime = Time.deltaTime;

            if (isOwner) {
                Power_OwnerUpdate(deltaTime);
                Fault_OwnerUpdate(deltaTime);
            }

            Power_Update(deltaTime);
            Fault_Update();
            Sound_Update(deltaTime);
            Effect_Update();
            JetBrust_Update();

            var stopped = Mathf.Approximately(n2 + n1, 0) && Mathf.Approximately(egt, externalTempurature);
            if (!hasPilot && stopped) {
                if (isOwner) airVehicle.EngineOutput = 0;
                gameObject.SetActive(false);
            }
        }

        public override void PostLateUpdate() {
            PlayerStrike_Update();
        }

        private void ResetStatus() {
            Power_Reset();
            Fault_Reset();
            Fault_Update();
            Sound_Reset();
            Effect_Update();
            JetBrust_Update();
        }

        public void ToggleEngine() {
            //当发动机主电门状态变化
            if (fuel || starter)//断油
            {
                FuelCutoff();
                DisengageStarter();
            }                
            else
                EngageStarter();
        }

        public void _InstantStart() {
            starter = false;
            fuel = true;
            n2 = idleN2;
            n1 = idleN1;
        }

    #endregion

    #region Power

        [Header("Power")]
        [Tooltip("[N]")] public float maxThrust = 130408.51f;
        [Tooltip("adjust engine thrust force postion")]
        public GameObject forcePosition;
        public float thrustCurve = 2.0f;
        [Tooltip("current thrust")]
        public float normalizedThrust = 0f;
        [Header("N1")]
        [Tooltip("[rpm]")] public float minN1 = 307.9f;

        [Tooltip("[rpm]")] public float idleN1 = 879.6f;
        [Tooltip("[rpm]")] public float referenceN1 = 4397;
        [Tooltip("[rpm]")] public float continuousN1 = 4397;
        [Tooltip("[rpm]")] public float takeOffN1 = 4586;
        public float n1Response = 0.1f;
        public float n1DecreaseResponse = 0.08f;
        public float n1StartupResponse = 0.01f;

        [Header("N2")]
        [Tooltip("[rpm]")] public float minN2 = 3433.4f;

        [Tooltip("[rpm]")] public float idleN2 = 8583.5f;
        [Tooltip("[rpm]")] public float referenceN2 = 17167;
        [Tooltip("[rpm]")] public float continuousN2 = 17167;
        [Tooltip("[rpm]")] public float takeOffN2 = 20171;

        public float n2Responce = 0.05f;
        public float n2DecreaseResponse = 0.04f;
        public float n2StartupResponse = 0.005f;

        [Header("EGT")]
        [Tooltip("未点火时，依靠压气机出口的最大EGT[℃]")] public float compressorEGT = 55;

        [Tooltip("[℃]")] public float idleEGT = 725;
        [Tooltip("[℃]")] public float continuousEGT = 1013;
        [Tooltip("[℃]")] public float takeOffEGT = 1038;
        [Tooltip("[℃]")] public float fireEGT = 1812;
        public float egtResponse = 0.02f;

        [Header("Fuel")]
        //启动过程中核心机相对换算转速
        public float[] startUpN2rLaw = { 0.20f, 0.23f, 0.25f, 0.60f };

        [Tooltip("启动过程供油控制律（公斤每小时）")]
        public float[] startUpFFLaw = { 20, 200, 160, 400 }; //乘以20是公斤每小时的数值

        [Tooltip("最大状态耗油率（公斤每小时）")]
        public float takeOffFF = 5060;

        [Header("ECT")]
        [Tooltip("[℃]")] public float idleECT = 196;

        [Tooltip("[℃]")] public float continuousECT = 274;
        [Tooltip("[℃]")] public float overheatECT = 343;
        [Tooltip("[℃]")] public float fireECT = 850;
        public float ectResponse = 0.1f;
        public float ectOverheatResponse = 0.001f;

        [Header("Oil")]
        [Tooltip("[℃]")] public float idleOilTempurature = 31;

        [Tooltip("[℃]")] public float maxOilTempurature = 140;
        [Tooltip("[℃]")] public float takeOffOilTempurature = 155;
        [Tooltip("[hPa]")] public float idleOilPressure = 1200;
        [Tooltip("[hPa]")] public float maxOilPressure = 2000;

        [Header("Starter")]
        public bool autoRelease = true;
        public bool autoFuel = true;

        [Header("Reverser")]
        public float reverserRatio = 0.6f;
        public float reverserExtractResponse = 0.5f;
        public float reverserRetractResponse = 0.5f;
        [NonSerialized] public float idlePoint = 0.375f;

        // [Header("Runtime Synced Variables")]
        [NonSerialized] [UdonSynced] public bool reversing, starter, fuel;
        [NonSerialized] [UdonSynced] public float n1, n2, egt, ect, ff, throttleLeveler;

        [NonSerialized] public float throttleInput, oilTempurature, oilPressure;

        [NonSerialized] public float reverserPosition;
        
        //private DFUNC_Brake brake;
        private Rigidbody vehicleRigidbody;
        //private bool hasWheelCollider;
        private Animator vehicleAnimator;
        private string gripAxis;


        public void EngageStarter() {
            starter = true;
        }

        public void DisengageStarter() {
            starter = false;
        }

        public void ToggleStarter() {
            starter = !starter;
        }

        public void FuelOn() {
            fuel = true;
        }

        public void FuelCutoff() {
            fuel = false;
        }

        public void ToggleFuel() {
            fuel = !fuel;
        }

        private void Power_Start() {
            //启动过程中几个转速关键帧
            startUpN2rLaw[0] *= takeOffN2;
            startUpN2rLaw[1] *= takeOffN2;
            startUpN2rLaw[2] *= takeOffN2;
            startUpN2rLaw[3] *= takeOffN2;

            //实现空客反推所需的ThrottleInput设置
            idlePoint = reverserRatio / (1 + reverserRatio);
            //brake = entity.GetComponentInChildren<DFUNC_Brake>(true);
            //apu = entity.GetComponentInChildren<SFEXT_AuxiliaryPowerUnit>(true);
            //hasWheelCollider = entity.GetComponentInChildren<WheelCollider>(true) != null;
            Power_Reset();
        }

        private void Power_Reset() {
            gripAxis = airVehicle.SwitchHandsJoyThrottle
                        ? "Oculus_CrossPlatform_SecondaryHandTrigger"
                        : "Oculus_CrossPlatform_PrimaryHandTrigger";

            starter = false;
            fuel = false;
            reversing = false;
            reverserPosition = 0;
            throttleLeveler = idlePoint;
            airVehicle.ThrottleInput = idlePoint;
            n1 = 0;
            n2 = 0;
            egt = externalTempurature;
            ect = externalTempurature;
            oilTempurature = externalTempurature;
            oilPressure = 1100;
            ff = 0;
            isAutoThrustActive = false;
            autoThrustInput = 0f;

            Update_Animator();
            
        }

        private void Power_OwnerFixedUpdate() {
            var thrust = normalizedThrust * maxThrust * Mathf.Lerp(1, -reverserRatio, reverserPosition * 2.0f - 1.0f);
            vehicleRigidbody.AddForceAtPosition(transform.forward * thrust, forcePosition.transform.position, ForceMode.Force);
        }

        private float Power_GetThrottleInput() {
            var reverserInterlocked = reversing && reverserPosition < 0.5f;

            float input;
            if (reversing) {
                throttleLeveler = Mathf.Clamp(airVehicle.ThrottleInput, 0, idlePoint + 0.02f);
                airVehicle.ThrottleInput = throttleLeveler;
                airVehicle.PlayerThrottle = airVehicle.ThrottleInput;
                input = airVehicle.ThrottleOverridden > 0 && Input.GetAxis(gripAxis) < 0.75f &&
                        airVehicle.ThrottleInput > idlePoint + 0.02f
                    ? 0f
                    : idlePoint - airVehicle.ThrottleInput;
            }
            else {
                throttleLeveler = Mathf.Clamp(airVehicle.ThrottleInput, idlePoint, 1);
                airVehicle.ThrottleInput = throttleLeveler;
                airVehicle.PlayerThrottle = airVehicle.ThrottleInput;
                input = airVehicle.ThrottleOverridden > 0 && Input.GetAxis(gripAxis) < 0.75f
                    ? (airVehicle.ThrottleOverride - idlePoint) / (1 - idlePoint)
                    : (airVehicle.ThrottleInput - idlePoint) / (1 - idlePoint);
            }

            if (isAutoThrustActive && autoThrustInput <= input) {
                return autoThrustInput;
            }

            input = reverserInterlocked ? 0.0f : input;

            return input;
        }

        private void Power_OwnerUpdate(float deltaTime) {
            throttleInput = Power_GetThrottleInput();
            throttleLeveler = airVehicle.ThrottleInput;
            //throttleInput = reverserInterlocked ? 0.0f : (airVehicle.ThrottleOverridden > 0 && Input.GetAxis(gripAxis) < 0.75f ? airVehicle.ThrottleOverride : airVehicle.ThrottleInput);

            var isStarterAvailable = starter && aircraftSystemData.hasBleedAir;
            var isN2Runnning = fuel && n2 >= minN2 && !stall;
            var targetN2 = isStarterAvailable || isN2Runnning
                ? Mathf.Lerp(fuel ? idleN2 : minN2 * 1.1f, takeOffN2, throttleInput) *
                  ClampedRemap01(airVehicle.Fuel, 0, airVehicle.LowFuel)
                : 0.0f;
            n2 = TowWayMoveTowards(n2, targetN2, deltaTime * continuousN2 * Randomize(),
                isN2Runnning ? n2Responce : n2StartupResponse, n2DecreaseResponse);

            //启动逻辑：N2到达16%时，N1才启转
            var targetN1 = !isN2Runnning
                ? Lerp3(0, 0, idleN1, n2, 0, 0.16f * takeOffN2, idleN2)
                : Lerp3(0, idleN1, takeOffN1, n2, 0, idleN2, takeOffN2);
            //启动逻辑：n1 < minN1 时，使用启动过程的响应速度
            //TODO 现在的写法有N1前期响应快后期响应慢的问题
            //n1 = Mathf.MoveTowards(n1, targetN1, deltaTime * (Mathf.Lerp(n1StartupResponse, n1Response, ClampedRemap01(n1, 0, idleN1))) * continuousN1 * Randomize());
            n1 = Mathf.MoveTowards(n1, targetN1,
                deltaTime * (n1 < idleN1 ? n1StartupResponse : n1Response) * continuousN1 * Randomize());

            normalizedThrust = Mathf.Clamp01(Mathf.Pow(n1 / takeOffN1, thrustCurve));

            var minEGT = Mathf.Lerp(externalTempurature, compressorEGT, ClampedRemap01(n2, 0, minN2));
            var egtTarget = fire
                ? fireEGT
                : Lerp4(minEGT, fuel ? idleEGT : minEGT, continuousEGT, takeOffEGT, n2, 0, idleN2, continuousN2,
                    takeOffN2);
            egt = Mathf.Lerp(egt, egtTarget, deltaTime * egtResponse * Randomize());

            var ffTarget = starter
                ? n2 > startUpN2rLaw[0]  
                    ? Lerp4(startUpFFLaw[0], startUpFFLaw[1], startUpFFLaw[2], startUpFFLaw[3], n2, startUpN2rLaw[0], startUpN2rLaw[1],
                        startUpN2rLaw[2], startUpN2rLaw[3])
                    : 0
                : Convert.ToInt32(fuel) * Mathf.Lerp(0.95f * startUpFFLaw[3], takeOffFF, throttleInput);
            //启动时的峰值油量400，慢车380
            ff = Mathf.Lerp(ff, ffTarget, starter ? 1 : deltaTime * n1Response);

            var ectTarget = Lerp4(externalTempurature, idleECT, continuousECT, egt, egt, externalTempurature, idleEGT,
                continuousEGT, takeOffEGT);
            ect = Mathf.Lerp(ect, ectTarget,
                deltaTime * (egt <= continuousEGT || fire ? ectResponse : ectOverheatResponse) * Randomize());


            //airVehicle.EngineOutput = normalizedThrust;

            if (starter && autoFuel && n2 >= minN2 && !fuel) fuel = true;
            if (starter && autoRelease && fuel && n2 >= minN2 * 2.27f) starter = false;
        }

        private void Power_Update(float deltaTime) {
            //reverserPosition = TowWayMoveTowards(reverserPosition, reversing ? 1 : 0, deltaTime, reverserExtractResponse, reverserRetractResponse);
            reverserPosition = TowWayMoveTowards(reverserPosition, reversing ? 0.99f : 0, deltaTime,
                reverserExtractResponse, reverserRetractResponse);
            oilTempurature = Lerp4(externalTempurature, idleOilTempurature, maxOilTempurature, takeOffOilTempurature,
                ect, externalTempurature, idleECT, continuousECT, Mathf.Max(egt, continuousEGT));
            oilPressure = Lerp3(gasProperty.PressuerStatic/10000f, idleOilPressure, maxOilPressure, n2, 0, idleN2, takeOffN2);
            Update_Animator();
        }

    #endregion

    #region Sound

        [Header("Sounds")]
        public AudioSource idleSound;

        public AudioSource insideSound;
        public AudioSource thrustSound;
        public AudioSource takeOffSound;
        public float soundResponse = 1.0f;

        private SAV_SoundController soundController;

        private float idleVolume, insideVolume, thrustVolume, takeOffVolume;

        private void Sound_Start(SaccEntity entity) {
            soundController = entity.GetComponentInChildren<SAV_SoundController>();
            Sound_Reset();
        }

        private void Sound_Update(float deltaTime) {
            var currentThrottleLevelerSlot = aircraftSystemData.throttleLevelerSlot;

            if (currentThrottleLevelerSlot != _lastThrottleLevelerSlot && airVehicle.IsOwner) {
                Networking.LocalPlayer.PlayHapticEventInHand(
                    airVehicle.SwitchHandsJoyThrottle ? VRC_Pickup.PickupHand.Right : VRC_Pickup.PickupHand.Left, 0.2f,
                    1f, 0.1f);

                if ((_lastThrottleLevelerSlot != ThrottleLevelerSlot.Revers ||
                     _lastThrottleLevelerSlot != ThrottleLevelerSlot.IDLERevers) &&
                    (currentThrottleLevelerSlot == ThrottleLevelerSlot.IDLERevers ||
                     currentThrottleLevelerSlot == ThrottleLevelerSlot.Revers)) {
                    throttleLevelerAudioSource.PlayOneShot(revOnClip);
                }
                else if (currentThrottleLevelerSlot != ThrottleLevelerSlot.TOGA &&
                         _lastThrottleLevelerSlot == ThrottleLevelerSlot.TOGA) {
                    throttleLevelerAudioSource.PlayOneShot(togaReleaseClip);
                }
                else {
                    switch (currentThrottleLevelerSlot) {
                        case ThrottleLevelerSlot.TOGA:
                            throttleLevelerAudioSource.PlayOneShot(togaClip);
                            break;
                        case ThrottleLevelerSlot.CLB:
                            throttleLevelerAudioSource.PlayOneShot(clbClip);
                            break;
                        case ThrottleLevelerSlot.FlexMct:
                            throttleLevelerAudioSource.PlayOneShot(mctFlexClip);
                            break;
                        case ThrottleLevelerSlot.IDLE:
                            throttleLevelerAudioSource.PlayOneShot(idleClip);
                            break;
                        case ThrottleLevelerSlot.IDLERevers:
                            throttleLevelerAudioSource.PlayOneShot(idleClip);
                            break;
                        case ThrottleLevelerSlot.MaxRevers:
                            throttleLevelerAudioSource.PlayOneShot(revMaxClip);
                            break;
                        default:
                            throttleLevelerAudioSource.PlayOneShot(revIdleClip);
                            break;
                    }

                    if (currentThrottleLevelerSlot == ThrottleLevelerSlot.TOGA) {
                        throttleLevelerAudioSource.PlayOneShot(togaClip);
                    }
                }

                _lastThrottleLevelerSlot = currentThrottleLevelerSlot;
            }

            var isInside = (isPilot || isPassenger) && soundController.AllDoorsClosed;
            var doppler = isInside ? 1.0f : Mathf.Min(soundController.Doppler, 2.25f);
            var silent = soundController.silent;
            var n2ToIdle = Remap01(n2, 0, idleN2);
            var n1ToIdle = Remap01(n1, 0, idleN1);
            SetAudioVolumeAndPitch(idleSound, isInside ? 0.0f : n2ToIdle * idleVolume,
                Lerp3(0.0f, 0.23f, 1.1f, n2, 0.0f, idleN2, continuousN2) * doppler, soundResponse * deltaTime);
            SetAudioVolumeAndPitch(insideSound, isInside ? n1ToIdle * insideVolume : 0,
                Lerp3(0.0f, 0.45f, 1.5f, n1, 0, idleN1, takeOffN1), soundResponse * deltaTime);
            SetAudioVolumeAndPitch(thrustSound,
                n1ToIdle * thrustVolume * ClampedRemap01(n1, idleN1, takeOffN1) * (isInside ? 0.09f : 1.0f) *
                (silent ? 0.0f : 1.0f) * doppler, 1, soundResponse * deltaTime);
            SetAudioVolumeAndPitch(takeOffSound,
                n1ToIdle * takeOffVolume * ClampedRemap01(n1, continuousN1, takeOffN1) * (isInside ? 0.09f : 1.0f) *
                (silent ? 0.0f : 1.0f) * doppler, 1, soundResponse * deltaTime);
        }

        private void Sound_Reset() {
            MuteAudioSources(soundController.PlaneIdle);
            MuteAudioSources(soundController.Thrust);
            MuteAudioSource(soundController.PlaneInside);

            if (InitializeAudioSource(idleSound)) idleVolume = idleSound.volume;
            if (InitializeAudioSource(insideSound)) insideVolume = insideSound.volume;
            if (InitializeAudioSource(takeOffSound)) takeOffVolume = takeOffSound.volume;
            if (InitializeAudioSource(thrustSound)) thrustVolume = thrustSound.volume;
        }

    #endregion

    #region Effect && Animator

        [Header("Effects")]
        public ParticleSystem fireEffect;

        public ParticleSystem thrustEffect;
        private float fireStartSpeed, thrustStartSpeed;

        public GameObject agent1DischIndicator;

        private void Effect_Start() {
            fireStartSpeed = fireEffect.main.startSpeedMultiplier;
            thrustStartSpeed = thrustEffect.main.startSpeedMultiplier;
        }

        private void Effect_Update() {
            SetParticleEmission(fireEffect, fire, fireStartSpeed * Mathf.Max(n2 / takeOffN2, 0.1f));
            SetParticleEmission(thrustEffect, !fire && egt - externalTempurature > 15.0f,
                thrustStartSpeed * Mathf.Max(n1 / takeOffN1, 0.1f));
        }

        private void Update_Animator() {
            if (vehicleAnimator) {
                vehicleAnimator.SetBool("reverse", reversing);
                vehicleAnimator.SetFloat("reverser", reverserPosition);
                vehicleAnimator.SetFloat("throttleleveler", throttleLeveler);
            }

        }

        #endregion

    #region Fault

        [Header("Fault")]
        public float mtbFireAtContinuous = 30 * 24 * 60 * 60;

        public float mtbFireAtOverheat = 90;
        public float mtbFireAtFire = 10;
        public float mtbMeltdownOnFire = 90;

        [NonSerialized] [UdonSynced] public bool fire;
        [NonSerialized] public bool overheat, stall, broken, dished;

        public void Dish() {
            dished = true;
            agent1DischIndicator.SetActive(dished);
        }

        private void Fault_Start() {
            Fault_Reset();
        }

        private void Fault_Reset() {
            fire = false;
            stall = false;
            dished = false;
            agent1DischIndicator.SetActive(dished);
            broken = false;
        }

        private void Fault_OwnerUpdate(float deltaTime) {
            if (!fire && !dished && Random.value < deltaTime / Lerp3(mtbFireAtContinuous, mtbFireAtOverheat,
                    mtbFireAtFire, ect, continuousECT, overheatECT, fireECT)) fire = true;

            if (fire && dished) fire = false;

            if (ect > fireECT && !stall && Random.value < deltaTime / mtbMeltdownOnFire) broken = true;

            if (broken) stall = true;
        }

        private void Fault_Update() {
            overheat = ect > overheatECT;
        }

    #endregion

    #region Player Strike

        [Header("Player Strike")]
        public bool playerStrike = true;

        public float inletOffset = 2.0f;
        public float inletAreaIdleRange = 3.1f;
        public float inletAreaTakeOffRange = 4.2f;
        public float inletAreaAngle = 40.0f;
        public float exhaustAreaIdleRange = 60.0f;
        public float exhaustAreaTakeOffRange = 100.0f;
        public float exhaustAreaExtent = 8.0f;
        public float exhaustAreaAngle = 30.0f;
        public float idlePlayerAcceleration = 100;
        public float takeOffPlayerAcceleration = 1000;
        public float strikeDistance = 3.0f;
        public AudioSource strikeSoundSource;
        public AudioClip strikeSound;

        private void PlayerStrike_Update() {
            var localPlayer = Networking.LocalPlayer;
            // if (isPilot)
            // {
            //     var vehicleVelocity = vehicleRigidbody.velocity;
            //     if (Vector3.Distance(localPlayer.GetVelocity(), vehicleVelocity) > 1)
            //     {
            //         localPlayer.SetVelocity(vehicleVelocity);
            //     }
            //     return;
            // }

            if (!Utilities.IsValid(localPlayer) || !playerStrike || isPilot || isPassenger || onBoarding ||
                Mathf.Approximately(n1, 0)) return;

            var playerPosition = localPlayer.GetPosition();

            var exhaustPlayerPosition = transform.InverseTransformPoint(playerPosition);
            var inletPlayerPosition = exhaustPlayerPosition - Vector3.forward * inletOffset;
            var inletDistance = inletPlayerPosition.magnitude;
            var inletDirection = inletPlayerPosition / inletDistance;

            if (inletDirection.z > 0 &&
                inletDistance < Lerp3(0, inletAreaIdleRange, inletAreaTakeOffRange, n1, 0, idleN1, takeOffN1) &&
                Mathf.Abs(Vector3.SignedAngle(Vector3.forward, inletDirection, Vector3.up)) < inletAreaAngle) {
                if (inletDistance < strikeDistance) {
                    PlayStrikeSound();
                    SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(PlayerStrike));
                }

                AddPlayerForce(localPlayer,
                    -transform.TransformDirection(inletDirection) * Mathf.Lerp(idlePlayerAcceleration,
                        takeOffPlayerAcceleration, normalizedThrust));
            }
            else {
                var exhaustDistance = exhaustPlayerPosition.magnitude;
                var exhaustDirection = exhaustPlayerPosition / exhaustDistance;

                if (exhaustDirection.z < 0 && Mathf.Abs(exhaustPlayerPosition.x) < exhaustAreaExtent &&
                    exhaustDistance < Lerp3(0, exhaustAreaIdleRange, exhaustAreaTakeOffRange, n1, 0, idleN1,
                        takeOffN1) &&
                    Mathf.Abs(Vector3.SignedAngle(Vector3.back, exhaustDirection, Vector3.up)) < exhaustAreaAngle)
                    AddPlayerForce(localPlayer,
                        -transform.forward * Mathf.Lerp(idlePlayerAcceleration, takeOffPlayerAcceleration,
                            normalizedThrust));

                if (exhaustDirection.z > 0 && reverserPosition > 0.5f &&
                    Mathf.Abs(exhaustPlayerPosition.x) < exhaustAreaExtent &&
                    exhaustDistance < Lerp3(0, exhaustAreaIdleRange, exhaustAreaTakeOffRange, n1, 0, idleN1,
                        takeOffN1) &&
                    Mathf.Abs(Vector3.SignedAngle(Vector3.forward, exhaustDirection, Vector3.up)) < exhaustAreaAngle)
                    AddPlayerForce(localPlayer,
                        transform.forward * Mathf.Lerp(idlePlayerAcceleration, takeOffPlayerAcceleration,
                            normalizedThrust));
            }
        }

        public void PlayerStrike() {
            if (!broken) {
                broken = true;
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayStrikeSound));
            }

            fire = true;
        }

        public void PlayStrikeSound() {
            if (strikeSoundSource && strikeSound) strikeSoundSource.PlayOneShot(strikeSound);
        }

        private void AddPlayerForce(VRCPlayerApi player, Vector3 force) {
            player.SetVelocity(player.GetVelocity() +
                               (force + (player.IsPlayerGrounded() ? Vector3.up * 0.5f : Vector3.zero)) *
                               Time.deltaTime);
        }

    #endregion

    #region Jet Brust

        [Header("Jet Brust")]
        public ParticleSystem brustParticle;

        public ParticleSystem reverserBrustParticle;
        public float brustIdleSpeed = 10;
        public float brustTakeOffSpeed = 1000;
        private bool onBoarding;

        private void JetBrust_Start() {
            SetParticleEmission(brustParticle, false, 0);
            SetParticleEmission(reverserBrustParticle, false, 0);
        }

        private void JetBrust_Update() {
            SetParticleEmission(brustParticle, !Mathf.Approximately(n1, 0),
                Lerp3(0, brustIdleSpeed, brustTakeOffSpeed, n1, 0, idleN1, takeOffN2));
            var reverserIntensiy = Mathf.Clamp01(reverserPosition * 2 - 1);
            SetParticleEmission(reverserBrustParticle, !Mathf.Approximately(n1 * reverserIntensiy, 0),
                Lerp3(0, brustIdleSpeed, brustTakeOffSpeed, n1, 0, idleN1, takeOffN2) * reverserIntensiy *
                reverserRatio);
        }

    #endregion

    #region SFUtilities

    #endregion

    #region GameObject Utilities

        private void SafeSetActive(GameObject gameObject, bool value) {
            if (gameObject && gameObject.activeSelf != value) gameObject.SetActive(value);
        }

        private void SafeSetActives(GameObject[] gameObjects, bool value) {
            if (gameObjects != null)
                foreach (var gameObject in gameObjects)
                    SafeSetActive(gameObject, value);
        }

    #endregion

    #region Effect Utilities

        private void SetParticleEmission(ParticleSystem system, bool emit, float speed) {
            if (!system) return;
            if (emit) {
                var main = system.main;
                main.startSpeedMultiplier = speed;
            }

            var emission = system.emission;
            if (emission.enabled != emit) emission.enabled = emit;
        }

        private void SetParticleCollisionMatrix(ParticleSystem particleSystem, LayerMask layerMask) {
            if (!particleSystem) return;
            var collision = particleSystem.collision;
            collision.collidesWith = layerMask;
        }

    #endregion

    #region Audio Utilities

        private bool InitializeAudioSource(AudioSource audioSource) {
            if (!audioSource) return false;
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            return true;
        }

        private void SetAudioVolumeAndPitch(AudioSource audioSource, float volume, float pitch, float response) {
            if (!audioSource) return;

            var stop = Mathf.Approximately(volume, 0.0f) || Mathf.Approximately(pitch, 0.0f);

            if (!stop) {
                audioSource.volume = Mathf.Lerp(audioSource.volume, volume, response);
                audioSource.pitch = Mathf.Lerp(audioSource.pitch, pitch, response);
            }

            if (audioSource.isPlaying == stop) {
                if (stop) {
                    audioSource.Stop();
                }
                else {
                    Debug.Log(audioSource.name);
                    audioSource.time = audioSource.clip.length * (Random.value % 1.0f);
                    audioSource.volume = volume;
                    audioSource.pitch = pitch;
                    audioSource.Play();
                }
            }
        }

        private void MuteAudioSources(AudioSource[] audioSources) {
            if (audioSources == null) return;
            foreach (var audioSource in audioSources) MuteAudioSource(audioSource);
        }

        private void MuteAudioSource(AudioSource audioSource) {
            if (!audioSource) return;
            audioSource.mute = true;
            audioSource.playOnAwake = false;
            audioSource.priority = 255;
            audioSource.Stop();
        }

    #endregion

    #region Math Utilities

        private float Randomize() {
            return 1 + (Random.value - 0.5f) * randomRange;
        }

        private float Lerp3(float a, float b, float c, float t, float tMin, float tMid, float tMax) {
            return Mathf.Lerp(a, Mathf.Lerp(b, c, Remap01(t, tMid, tMax)), Remap01(t, tMin, tMid));
        }

        private float Lerp4(float a, float b, float c, float d, float t, float tMin, float tMid1, float tMid2,
            float tMax) {
            return Mathf.Lerp(a, Mathf.Lerp(b, Mathf.Lerp(c, d, Remap01(t, tMid2, tMax)), Remap01(t, tMid1, tMid2)),
                Remap01(t, tMin, tMid1));
        }

        private float Remap01(float value, float oldMin, float oldMax) {
            return (value - oldMin) / (oldMax - oldMin);
        }

        private float ClampedRemap01(float value, float oldMin, float oldMax) {
            return Mathf.Clamp01(Remap01(value, oldMin, oldMax));
        }

        private float TowWayMoveTowards(float a, float b, float maxDelta, float ascMultiplier, float dscMultiplier) {
            return Mathf.MoveTowards(a, b, maxDelta * (a < b ? ascMultiplier : dscMultiplier));
        }

        // private float Remap(float value, float oldMin, float oldMax, float newMin, float newMax)
        // {
        //     return Remap01(value, oldMin, oldMax) * (newMax - newMin) + newMin;
        // }

        // private float ClampedRemap(float value, float oldMin, float oldMax, float newMin, float newMax)
        // {
        //     return ClampedRemap01(value, oldMin, oldMax) * (newMax - newMin) + newMin;
        // }

    #endregion
    }
}