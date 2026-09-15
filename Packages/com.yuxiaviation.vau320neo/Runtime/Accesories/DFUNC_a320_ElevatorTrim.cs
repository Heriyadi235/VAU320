using System;
using System.Diagnostics.Eventing.Reader;
using A320VAU.ADIRU;
using A320VAU.Common;
using SaccFlightAndVehicles;
//using Serilog.Filters;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using YuxiFlightInstruments.BasicFlightData;

//note:this code is original from https://github.com/esnya/EsnyaSFAddons
//to satisfy vau320's demand, add autotrim
//to optimize change vellift in SAV to trim
//2024-09-29 ����һ���¶������Ȱ�����ű�������JoystickOverridde�ϣ���FBW����
namespace A320VAU.DFUNC {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class DFUNC_a320_ElevatorTrim : UdonSharpBehaviour {

        public YFI_FlightDataInterface BasicFlightData;
        public RadioAltimeter.RadioAltimeter radioAltimeter;
        private ADIRU _adiru;
        [Header("��ƽ����")]
        //[Tooltip("�����ƽǿ�ȣ�������velLift����������320���Ծ���10 ��-10-10��-10ʱ��ͷ����΢���µ���")]
        //[Range(0, 50)] public float trimStrength = 10;
        //[Tooltip("��ƽǿ��ƫ�ã����յ�VelLift =trimStrength *x +  trimBias")]
        //[Range(0, 50)] public float trimBias = 8;
        
        private float prevTrim;
        
        public float initialTrim = -0.1f;
        [UdonSynced] public float trim;//��ǰ��ƽλ�ã�-1~1
        public float critiaclAOA = 20f;//�ٽ繥�ǣ�sav.pitchaoa���ڸ���ֵʱ������afloorProtect;

        [Header("controller")]
        public float targetLoadFactor = 1;
        public float targetAoa = 0;

        public float TrimError = 0;
        private float TrimErrorLastFrame = 0;
        public float TrimErrorIntergrate = 0;
        public float TrimErrorDerivative = 0;

        [Header("Controllor value for curise")]
        public float kp1 = 0.04f; //�غ�ϵ�������� 0.6 0.015 �����ǿ����� 0.02 0.001
        public float ki1 = 0.0015f;
        public float kd1 = 0.0001f;

        [Header("Controllor value for low speed (below 220kts)")]
        //�Ϳ�С���٣�ʹ����һ�׸��ȶ��Ĳ���
        public float kp2 = 0.25f; 
        public float ki2 = 0.4f;
        public float kd2 = 0.0003f;


        [Header("animation")]
        public string animatorParameterName = "elevtrim";

        [Header("Haptics")]
        public Vector3 vrInputAxis = Vector3.forward;
        [Range(0, 1)] public float hapticDuration = 0.2f;
        [Range(0, 1)] public float hapticAmplitude = 0.5f;
        [Range(0, 1)] public float hapticFrequency = 0.1f;

        [Header("Debug")]
        public Transform debugControllerTransform;
        [Tooltip("0-ֱ�ӷ��� 1-����ģʽ 2-����ģʽ 3-��ƽģʽ")]
        public int trimMode = 1; //0-ֱ�ӷ��� 1-����ģʽ 2-����ģʽ 3-��ƽģʽ
        public bool TrimActive = true; //�����(SFEXT_O_JoystickGrabbed/SFEXT_O_JoystickDropped)�Լ�AP(JoystickOverride)������ʱ����ƽ�ż���
        public bool TrimActiveLastFrame = false;
        public bool afloorProtect = false;
        public bool lowSpeedMode = false;

        public Vector3 FBWRotationInputs;

        private void ResetStatus() {
            //�Զ���ƽĬ�Ͽ���
            trimMode = 0;
            Dial_Funcon.SetActive(TrimActive);
            prevTrim = trim = initialTrim;
            if (vehicleAnimator) vehicleAnimator.SetFloat(animatorParameterName, .5f);
            //SAVControl.SetProgramVariable("VelLiftStart", trimStrength* trim + trimBias);
            vehicleRigidbody = SAVControl.VehicleRigidbody;
            TrimError = 0;
            TrimErrorIntergrate = 0;
            TrimErrorDerivative = 0;
            TrimErrorLastFrame = 0;
            targetAoa = 0;
    }

        private void PilotUpdate() {


            //������ƽֵ
            float DeltaTime = Time.deltaTime;

            var pitchInputs = SAVControl.RotationInputs.x;

            //����ģʽ
            if (TrimActive &&
                !SAVControl.Taxiing &&
                radioAltimeter.radioAltitude >= 50 ) {

                if (trimMode != 1) {
                    trimMode = 1;
                    TrimError = TrimErrorIntergrate = TrimErrorDerivative = 0f;
                    Debug.Log("[FBW]Flight Mode");
                }
                
                if(SAVControl.JoystickOverridden != 0) {
                    trim = initialTrim;
                }
                else { 
                    if(_adiru.adr.AOAPitch < critiaclAOA) {
                        afloorProtect = false;
                        targetLoadFactor = StickInputtoLoadFactor(pitchInputs, DeltaTime);
                        TrimError = (targetLoadFactor - _adiru.adr.verticalG);
                        TrimErrorIntergrate = Mathf.Clamp(TrimError * DeltaTime + TrimErrorIntergrate, -1, 1);//�������ֱ���
                        TrimErrorDerivative = (TrimError - TrimErrorLastFrame) / DeltaTime;
                    }
                    else {
                        afloorProtect = true;
                        targetAoa = StickInputtoAoa(pitchInputs, DeltaTime);
                        TrimError = targetAoa - _adiru.adr.AOAPitch;//todo:afloor������
                        TrimErrorIntergrate = 0;
                        TrimErrorDerivative = 0;
                    }
       
                    var kp = kp1;
                    var ki = ki1;
                    var kd = kd1;

                    lowSpeedMode = _adiru.adr.instrumentAirSpeed / 1.94384f < 110f;
                    if (lowSpeedMode) {
                        kp = kp2;
                        ki = ki2;
                        kd = kd2;
                    }

                        //trim = Mathf.MoveTowards(trim, Mathf.Clamp(kp * TrimError + ki * TrimErrorIntergrate, -1, 1), 0.1f);
                        trim = Mathf.Clamp(kp * TrimError + ki * TrimErrorIntergrate + kd * TrimErrorDerivative, -1, 1);
                    TrimErrorLastFrame = TrimError;
                }

            }

            //����ģʽ
            else if (TrimActive && SAVControl.Taxiing) {
                if (trimMode != 2) {
                    trimMode = 2;
                    TrimError = TrimErrorIntergrate = TrimErrorDerivative = 0f;
                    Debug.Log("[FBW]Ground Mode");
                }
                trim = initialTrim;
                targetLoadFactor = StickInputtoLoadFactor(pitchInputs, DeltaTime);
            }

            //��ƽģʽ
            else if (TrimActive &&
                radioAltimeter.radioAltitude < 50 &&
                !SAVControl.Taxiing &&
                _adiru.adr.verticalSpeed < -0.6 &&
                SAVControl.JoystickOverridden == 0) {

                var targetTrim = initialTrim;
                if (trimMode != 3) {
                    trimMode = 3;
                    Debug.Log("[FBW]Touchdown Mode");
                    targetTrim = trim - 0.05f;
                }
                //�����ǿ�����
                /*
                targetPitch = -2f;
                TrimError = (targetPitch - _adiru.irs.pitch);
                TrimErrorIntergrate += TrimError;
                TrimErrorDerivative = (TrimError - TrimErrorLastFrame) / DeltaTime;
                trim = Mathf.Clamp(kp * TrimError + ki * TrimErrorIntergrate + kd * TrimErrorDerivative, -1, 1);
                */
                TrimError = TrimErrorIntergrate = TrimErrorDerivative = 0f;
                trim = Mathf.MoveTowards(trim, targetTrim, DeltaTime * 0.025f);
            }
            
            //�ֶ���ƽ
            else if (!TrimActive) {
                var input = GetSliderInput();
                trim = Mathf.Clamp(trim + input, -1, 1);
                if (!Mathf.Approximately(input, 0) &&
                    Time.frameCount % Mathf.FloorToInt(hapticDuration / Time.fixedDeltaTime) == 0) PlayHapticEvent();
            }
            

        }

        private float StickInputtoLoadFactor(float pitchInputs, float deltaTime) {
            var maxLoad = 2f;
            var minLoad = 0f;

            var maxLoadRate = 1f * deltaTime;//ÿ�����仯1g
            //SAV�ű����Ѿ����������ƽ��������������ֱ������ת��Ϊ�غ�Ŀ��
            if (pitchInputs > 0.01) {//�Ƹ� 
                targetLoadFactor = Mathf.MoveTowards(targetLoadFactor,
                   (minLoad - 1f) * Mathf.Pow(pitchInputs, 2) + 1,
                    maxLoadRate);
            }
            else if ((pitchInputs < -0.01)) {//���� 
                targetLoadFactor = Mathf.MoveTowards(targetLoadFactor,
                     (maxLoad - 1f) * Mathf.Pow(pitchInputs, 2) + 1,
                    maxLoadRate);
            }
            else {
                targetLoadFactor = Mathf.MoveTowards(targetLoadFactor, 1, maxLoadRate);
            }
            if (trimMode != 1)
                //����ģʽ�����һ���Ƚ�С���غ�Ŀ��Լ�����������ʱģʽ�л�������ƽλ��ͻ��
                return Mathf.Clamp(targetLoadFactor, 1f - 0.5f, 1f + 0.5f);
            else
                return targetLoadFactor;
        }

        private float StickInputtoAoa(float pitchInputs, float deltaTime) {
           
            var maxLoadRate = 10f * deltaTime;//ÿ�����仯1��λ
            //SAV�ű����Ѿ����������ƽ��������������ֱ������ת��Ϊ�غ�Ŀ��
            if (pitchInputs > 0.01) {//�Ƹ� 
                targetAoa = Mathf.MoveTowards(targetAoa,
                    -(critiaclAOA) * Mathf.Pow(pitchInputs, 2),
                    maxLoadRate);
            }
            else if ((pitchInputs < -0.01)) {//����
                targetAoa = Mathf.MoveTowards(targetAoa,
                    (critiaclAOA) * Mathf.Pow(pitchInputs, 2),
                    maxLoadRate);
            }
            else {
                targetAoa = Mathf.MoveTowards(targetAoa, 1, maxLoadRate);
            }
            //if (trimMode != 1) return Mathf.Clamp(targetAoa, -3, 3);
            //else
                return targetAoa;
        }
        
        private void LocalUpdate() {
            var trimChanged = !Mathf.Approximately(trim, prevTrim);
            prevTrim = trim;
            if (trimChanged) {
                SetDirty();
                if (vehicleAnimator) vehicleAnimator.SetFloat(animatorParameterName, Remap01(trim, -1, 1));
                //SAVControl.SetProgramVariable("VelLiftStart", trim * trimStrength + trimBias);
                DebugOut.text = "FBW[WIP]\n[F6]\n" + (trim).ToString("f2") + (TrimActive ? "\nAuto": "\n");
            }
        }

        private void FixedUpdate() {
            if (!isOwner) return;

            var rotlift = Mathf.Clamp((_adiru.adr.instrumentAirSpeed / 1.94384f) / rotMultiMaxSpeed, -1, 1);
            //var DeltaTime = Time.fixedDeltaTime;
            vehicleRigidbody.AddForceAtPosition((trim * SAVControl.PitchStrength) * rotlift * SAVControl.Atmosphere * -transform.up, transform.position, ForceMode.Force);
            
            //�����˲�ͬ����������ʽ
            //1.��VelLiftStart
            //SAVControl.SetProgramVariable("VelLiftStart", trim * trimStrength + trimBias);
            //2.AddForceAtPosition

            //Vector3 trimPitching = Vector3.zero;
            //trim *= SAVControl.PitchStrength;
            //trim *= rotlift * Mathf.Min(SAVControl.AoALiftPitch, SAVControl.AoALiftYaw);
            //var downspeed = -Vector3.Dot(SAVControl.AirVel, SAVControl.VehicleTransform.up);
            //trimPitching = ((((SAVControl.VehicleTransform.up * trim) + (SAVControl.VehicleTransform.up * downspeed * SAVControl.VelStraightenStrPitch * SAVControl.AoALiftPitch * rotlift)) * SAVControl.Atmosphere));
            //vehicleRigidbody.AddForceAtPosition(trimPitching, transform.position, ForceMode.Force);//deltatime is built into ForceMode.Force

            //3.дJoystickOverride
            //FBWRotationInputs.x = Mathf.Clamp(trim, -1, 1);
            //FBWRotationInputs.y = 0;
            //FBWRotationInputs.z = 0;
            //SAVControl.SetProgramVariable("JoystickOverride", FBWRotationInputs);
        }
        public void TrimUp() {
            trim += desktopStep;
        }

        public void TrimDown() {
            trim -= desktopStep;
        }

        private void PlayHapticEvent() {
            var hand = trackingTarget == VRCPlayerApi.TrackingDataType.LeftHand
                ? VRC_Pickup.PickupHand.Left
                : VRC_Pickup.PickupHand.Right;
            Networking.LocalPlayer.PlayHapticEventInHand(hand, hapticDuration, hapticAmplitude, hapticFrequency);
        }

        private void ToggleAutoTrim() {
            if (!TrimActive) {
                TrimActive = true;
                Debug.Log("[FBW]AUTO TRIM");
            }
            else {
                TrimActive = false;
                Debug.Log("[FBW]MAN TRIM");
            }

            Dial_Funcon.SetActive(TrimActive);

            TrimError = 0;
            TrimErrorIntergrate = 0;
        }

        private float Remap01(float value, float oldMin, float oldMax) {
            return (value - oldMin) / (oldMax - oldMin);
        }

    #region DFUNC

        public float controllerSensitivity = 0.5f;
        public KeyCode desktopUp = KeyCode.T, desktopDown = KeyCode.Y;

        public float desktopStep = 0.02f;

        public KeyCode desktopEnableAuto = KeyCode.F6;
        public GameObject Dial_Funcon;
        public TextMeshPro DebugOut;
        private string triggerAxis;
        private VRCPlayerApi.TrackingDataType trackingTarget;

        public SaccEntity entityControl;
        public SaccAirVehicle SAVControl;
        private Transform controlsRoot;
        private Rigidbody vehicleRigidbody;
        private Animator vehicleAnimator;
        private bool hasPilot, isPilot, isOwner, isSelected, isDirty, triggered, prevTriggered;
        private bool InVR;
        private bool triggerLastFrame;
        private Vector3 prevTrackingPosition;
        private float sliderInput;
        private float rotMultiMaxSpeed;
        private float triggerTapTime = 1;

        public void DFUNC_LeftDial() {
            triggerAxis = "Oculus_CrossPlatform_PrimaryIndexTrigger";
            trackingTarget = VRCPlayerApi.TrackingDataType.LeftHand;
        }

        public void DFUNC_RightDial() {
            triggerAxis = "Oculus_CrossPlatform_SecondaryIndexTrigger";
            trackingTarget = VRCPlayerApi.TrackingDataType.RightHand;
        }

        public void DFUNC_Selected() {
            gameObject.SetActive(true);
            isSelected = true;
            prevTriggered = false;
        }

        public void DFUNC_Deselected() {
            gameObject.SetActive(trimMode > 0);
            isSelected = false;
            triggerTapTime = 1;
        }

        public void SFEXT_L_EntityStart() {
            _adiru = DependenciesInjector.GetInstance(this).adiru;
            controlsRoot = SAVControl.ControlsRoot;
            rotMultiMaxSpeed = SAVControl.RotMultiMaxSpeed;
            if (!controlsRoot) controlsRoot = entityControl.transform;
            vehicleAnimator = SAVControl.VehicleAnimator;
            ResetStatus();
        }

        public void SFEXT_O_PilotEnter() {
            isPilot = true;
            isOwner = true;
            isSelected = false;
            prevTriggered = false;
        }

        public void SFEXT_O_PilotExit() {
            isPilot = false;
            triggerTapTime = 1;
            isSelected = false;
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



        private void OnEnable() {
            triggerLastFrame = true;
        }

        private void OnDisable() {
            isSelected = false;
        }

        private void Update() {
            isDirty = false;
            if (isPilot) PilotUpdate();
            LocalUpdate();
            if (!hasPilot && !isDirty) gameObject.SetActive(false);
        }

        public override void PostLateUpdate() {
            if (isPilot) 
            {
                prevTriggered = triggered;
                triggered = (isSelected && Input.GetAxis(triggerAxis) > 0.75f) || debugControllerTransform;
                triggerTapTime += Time.deltaTime;
                
                if (triggered) 
                {
                    var trackingPosition =
                        controlsRoot.InverseTransformPoint(Networking.LocalPlayer.GetTrackingData(trackingTarget)
                            .position);
                    if (debugControllerTransform)
                        trackingPosition = controlsRoot.InverseTransformPoint(debugControllerTransform.position);

                    if (prevTriggered) 
                    {
                        sliderInput =
                            Mathf.Clamp(
                                Vector3.Dot(trackingPosition - prevTrackingPosition, vrInputAxis) *
                                controllerSensitivity, -1, 1);
                    }
                    else //enable and disable
                    {
                        if (triggerTapTime > .4f) //no double tap
                        {
                            triggerTapTime = 0;
                        }
                        else //double tap detected, switch trim
                        {
                            ToggleAutoTrim();
                            triggerTapTime = 1;
                        }
                    }

                    prevTrackingPosition = trackingPosition;
                }
                else {
                    sliderInput = 0;
                }

                if (Input.GetKeyDown(desktopUp)) sliderInput = desktopStep;
                if (Input.GetKeyDown(desktopDown)) sliderInput = -desktopStep;

                if (Input.GetKeyDown(desktopEnableAuto)) ToggleAutoTrim();
            }
        }

        private void SetDirty() {
            isDirty = true;
        }

        private float GetSliderInput() {
            //ʹ����sav�ı�Ƿ�����UP=-1 DOWN=1
            return -sliderInput;
        }

    #endregion
    }
}