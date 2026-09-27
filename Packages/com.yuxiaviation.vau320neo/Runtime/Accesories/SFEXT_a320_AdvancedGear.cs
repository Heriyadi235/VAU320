using System;
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

using Random = UnityEngine.Random;
using YuxiFlightInstruments;
using YuxiFlightInstruments.BasicFlightData;
using A320VAU.Brake;

//note:this code is original from https://github.com/esnya/EsnyaSFAddons
//to satisfy vau320's demand, moditied SteerAngle limiter

namespace A320VAU.SFEXT {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    [DefaultExecutionOrder(160)]//after electrical bus
    public class SFEXT_a320_AdvancedGear : UdonSharpBehaviour {
        public WheelCollider wheelCollider;
        public Transform suspensionTransform;
        public Transform steerTransform;
        public Transform wheelTransform;
        public Vector3 wheelUp = Vector3.up;
        public Vector3 wheelRight = Vector3.right;

        public YFI_FlightDataInterface basicFilghtData;

        [Header("Steering")]
        [Tooltip("deg")] public float maxSteerAngle;

        [Tooltip("最小转向角度,80节时达到 deg")] public float minSteerAngle;
        public float steerResponse = 1.0f;

        [Header("Brake")]
        [Tooltip("N/m")] public float brakeTorque = 10000;

        public float brakeResponse = 1.0f;
        public bool autoLimitGroundSpeed;
        public bool autoLimitGroundSpeedOnDesktop = true;

        [Header("Indicators")]
        public GameObject transitionIndicator;

        public GameObject downIndicator;


        [Header("Animations")]
        public string gearPositionParameterName = "gearpos";

        [Header("Sounds")]
        public AudioSource transitionSound;

        public AudioClip burstSound, breakSound;

        [Header("Failures")]
        [Tooltip("KIAS")] public float maxExtensionSpeed = 270;

        [Tooltip("KIAS")] public float maxRetractionSpeed = 235;
        [Tooltip("KIAS")] public float maxExtendedSpeed = 320;
        [Tooltip("KGS")] public float brakeMaxGroundSpeed = 60;

        public float mtbTransitionFail = 2 * 60 * 60;
        public float mtbTransitionFailOnOverspeed = 10;
        public float mtbTransitionBraek = 8 * 60 * 60;
        public float mtbTransitionBraekOnOverspeed = 60;
        public float mtbBurstOnOverGroundSpeed = 10;

        [Tooltip("feet/min")] public float verticalSpeedLimit = 600;
        [Tooltip("feet/min")] public float burstVerticalSpeed = 900;

        [Header("Effects")]
        public GameObject burstEffect;

        [Header("Misc")]
        public float timeNosieScale = 0.1f;

        
        [NonSerialized] public float maxSteeringCurrent;
        private bool prevIsGrounded = true;
        [NonSerialized] public float targetPosition;
        private Animator vehicleAnimator;
        private Rigidbody vehicleRigidbody;
        private float wheelAngle;
        private Vector3 wheelPositionOffset;
        private Quaternion wheelRotationOffset = Quaternion.identity, steerRotationOffset = Quaternion.identity;


        [UdonSynced][FieldChangeCallback(nameof(Bursted))]
        private bool _bursted;
        [NonSerialized][UdonSynced(UdonSyncMode.Smooth)] public float position;
        [NonSerialized][UdonSynced] public bool failed, broken, moving, inTransition;


        

        #region SFEXT Core
        
        private SaccAirVehicle airVehicle;
        public DFUNC_a320_Brake brakeFunction;

        
        private bool hasPilot, isOwner;
        private bool initialized;

        public void SFEXT_L_EntityStart() {

            airVehicle = basicFilghtData.SAVControl;

            airVehicle.DisableTaxiRotation_++;
            vehicleAnimator = airVehicle.VehicleAnimator;
            vehicleRigidbody = airVehicle.VehicleRigidbody;

            if (suspensionTransform)
                wheelPositionOffset = suspensionTransform.localPosition -
                                      suspensionTransform.parent.InverseTransformPoint(wheelCollider.transform
                                          .position);
            if (wheelTransform) wheelRotationOffset = wheelTransform.localRotation;
            if (steerTransform) steerRotationOffset = steerTransform.localRotation;

            gameObject.SetActive(false);
            initialized = true;

            ResetStatus();
        }

        public void SFEXT_O_PilotEnter() {
            isOwner = true;
        }

        public void SFEXT_O_TakeOwnership() {
            isOwner = true;
        }

        public void SFEXT_O_LoseOwnership() {
            isOwner = false;
        }

        public void SFEXT_G_PilotEnter() {
            hasPilot = true;
            gameObject.SetActive(true);
        }

        public void SFEXT_G_PilotExit() {
            hasPilot = false;
        }

        public void SFEXT_G_Explode() {
            ResetStatus();
        }

        public void SFEXT_G_RespawnButton() {
            ResetStatus();
        }

        public void SFEXT_G_GearUp() {
            targetPosition = 0;
            if (!gameObject.activeInHierarchy) vehicleAnimator.SetFloat(gearPositionParameterName, targetPosition);
        }

        public void SFEXT_G_GearDown() {
            targetPosition = 1;
            if (!gameObject.activeInHierarchy) vehicleAnimator.SetFloat(gearPositionParameterName, targetPosition);
        }

        public void SFEXT_G_TouchDown() { 
            return;
        }
        #endregion


        private bool Bursted {
            set {
                if (burstEffect) {
                    burstEffect.SetActive(value);
                }

                if (value && !_bursted && transitionSound) transitionSound.PlayOneShot(burstSound);
                wheelCollider.enabled = !value;
                _bursted = value;
            }
            get => _bursted;
        }

        private void Update() {
            if (!initialized) return;

            var deltaTime = Time.deltaTime;
            var taxiing = airVehicle.Taxiing;
            
            var groundSpeed = basicFilghtData.groundSpeed;
            if (isOwner) {
                inTransition = !Mathf.Approximately(position, targetPosition);
                moving = inTransition && !failed && !broken;
            }

            var retracted = !inTransition && Mathf.Approximately(position, 0);
            var extended = !inTransition && Mathf.Approximately(position, 1);
            var onGround = extended && taxiing;

            if (transitionIndicator && transitionIndicator.activeSelf != inTransition)
                transitionIndicator.SetActive(inTransition);
            if (downIndicator && downIndicator.activeSelf != extended) downIndicator.SetActive(extended);

            if (transitionSound && transitionSound.isPlaying != moving) {
                if (moving) transitionSound.PlayDelayed(Random.value * 0.1f);
                else transitionSound.Stop();
            }

            var targetBrakeStrength = GetTargetBrakeStrength(groundSpeed);
            var targetBrakeTorque = targetBrakeStrength * brakeTorque;
            var currentBrakeTorque = wheelCollider.brakeTorque;
            wheelCollider.brakeTorque = Mathf.MoveTowards(currentBrakeTorque, targetBrakeTorque,
                brakeTorque * brakeResponse * deltaTime);


            if (inTransition) vehicleAnimator.SetFloat(gearPositionParameterName, position);

            if (onGround && maxSteerAngle > 0) {
                var normalizedSteerAngle = onGround ? airVehicle.RotationInputs.y : 0;
                var targetSteerAngle = normalizedSteerAngle * GetTargetMaxSteerAngle(groundSpeed);
                wheelCollider.steerAngle = Mathf.MoveTowards(wheelCollider.steerAngle, targetSteerAngle,
                    deltaTime * steerResponse * maxSteerAngle);
            }

            if (!retracted) {
                if (suspensionTransform) {
                    Vector3 wheelPosition;
                    Quaternion _;
                    wheelCollider.GetWorldPose(out wheelPosition, out _);
                    suspensionTransform.localPosition =
                        suspensionTransform.parent.InverseTransformPoint(wheelPosition) * position +
                        wheelPositionOffset;
                }

                if (wheelTransform || steerTransform) {
                    var rpm = isOwner
                        ? wheelCollider.rpm
                        : groundSpeed * 0.514444f * 60.0f / (2 * wheelCollider.radius * Mathf.PI);
                    if (taxiing) wheelAngle = (wheelAngle + rpm * 360 / 60 * Time.deltaTime) % 360;
                    var steerRotation = Quaternion.AngleAxis(wheelCollider.steerAngle, wheelUp);
                    if (wheelTransform)
                        wheelTransform.localRotation = wheelRotationOffset *
                                                       (steerTransform ? Quaternion.identity : steerRotation) *
                                                       Quaternion.AngleAxis(wheelAngle, wheelRight);
                    if (steerTransform) steerTransform.localRotation = steerRotationOffset * steerRotation;
                }
            }

            if (isOwner) {
                if (!retracted) {
                    var ias = basicFilghtData.TAS;
                    var maxSpeed = _GetMaxSpeed();
                    var overspeed = maxSpeed > 0 && ias > maxSpeed;
                    var mtbfMultiplier = overspeed ? ias / maxSpeed : 1.0f;
                    var isGrounded = wheelCollider.isGrounded;

                    if (!broken && (inTransition || overspeed) && Random.value < deltaTime * mtbfMultiplier /
                        (overspeed ? mtbTransitionBraekOnOverspeed : mtbTransitionBraek)) broken = true;
                    if (!failed && inTransition && Random.value < deltaTime * mtbfMultiplier /
                        (overspeed ? mtbTransitionFailOnOverspeed : mtbTransitionFail)) failed = true;

                    if (brakeTorque > 0 && groundSpeed > brakeMaxGroundSpeed && isGrounded &&
                        groundSpeed / brakeMaxGroundSpeed * wheelCollider.brakeTorque / brakeTorque /
                        mtbBurstOnOverGroundSpeed * Time.deltaTime > Random.value) Burst();

                    var verticalSpeed = -basicFilghtData.verticalSpeed;
                    if (!prevIsGrounded && isGrounded) {
                        if (verticalSpeed > verticalSpeedLimit && Random.value <
                            (verticalSpeed - verticalSpeedLimit) / (burstVerticalSpeed - verticalSpeedLimit)) {
                            Burst();
                        }
                        else {
                            //TODO:根据速度判断是否有烟雾
                            burstEffect.SetActive(true);
                            SendCustomEventDelayedSeconds("DisableSmoke", 1f);
                        }
                    }
                    prevIsGrounded = wheelCollider.isGrounded;
                }

                if (!failed && !broken) {
                    var duration = transitionSound ? transitionSound.clip.length : 5.0f;
                    position = Mathf.MoveTowards(position, targetPosition,
                        Time.deltaTime / duration * Random.Range(1.0f - timeNosieScale, 1.0f + timeNosieScale));
                }
            }

            if (!hasPilot && !moving) gameObject.SetActive(false);
        }

        public void DisableSmoke() {
            if(!Bursted)    
                burstEffect.SetActive(false);
        }
        
        private void Burst() {
            Bursted = true;
            RequestSerialization();
        }

        public float _GetMaxSpeed() {
            if (Mathf.Approximately(position, 0)) return -1;

            if (Mathf.Approximately(position, 1)) return maxExtendedSpeed;

            return position < 0.5f ? maxExtensionSpeed : maxRetractionSpeed;
        }

        private float GetTargetMaxSteerAngle(float groundSpeed) {
            groundSpeed = groundSpeed * 1.94401f < 0 ? 0 : groundSpeed;
            //var SteerAngle = -1.23333f * groundSpeed + 98.66667f; //20节-74度 80节-0度
            var SteerAngle = -0.0027f * Mathf.Pow(groundSpeed, 2) - 0.6726f * groundSpeed + 81.269f;
            maxSteeringCurrent = Mathf.Clamp(SteerAngle, minSteerAngle, maxSteerAngle);
            return maxSteeringCurrent;
        }

        private void ResetStatus() {
            if (!initialized) return;
            targetPosition = position = 1.0f;
            if (transitionSound) transitionSound.Stop();
            failed = false;
            broken = false;
            vehicleAnimator.SetFloat(gearPositionParameterName, position);
            Bursted = false;
            burstEffect.SetActive(false);
        }

        private float GetTargetBrakeStrength(float groundSpeed) {
            if (Mathf.Approximately(position, 0.0f) || brakeFunction.ParkBreakSet) return 1.0f;
            if (!brakeFunction ||
                ((autoLimitGroundSpeed || (!Networking.LocalPlayer.IsUserInVR() && autoLimitGroundSpeedOnDesktop)) &&
                 groundSpeed >= brakeMaxGroundSpeed)) return 0;
            return brakeFunction.BrakeInput;
        }

    #region Math Utilities

        private float Remap01(float value, float oldMin, float oldMax) {
            return (value - oldMin) / (oldMax - oldMin);
        }

        private float ClampedRemap01(float value, float oldMin, float oldMax) {
            return Mathf.Clamp01(Remap01(value, oldMin, oldMax));
        }

    #endregion
    }
}