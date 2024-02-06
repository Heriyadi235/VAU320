using System;
using SaccFlightAndVehicles;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using YuxiFlightInstruments.BasicFlightData;

//note:this code is original from https://github.com/esnya/EsnyaSFAddons
//to satisfy vau320's demand, add autotrim
//to optimize change vellift in SAV to trim
namespace A320VAU.DFUNC {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class DFUNC_a320_ElevatorTrim : UdonSharpBehaviour {

        public YFI_FlightDataInterface BasicFlightData;
        public RadioAltimeter.RadioAltimeter radioAltimeter;
        [Header("��ƽ����")]
        [Tooltip("�����ƽǿ�ȣ�������velLift����������320���Ծ���10 ��-10-10��-10ʱ��ͷ����΢���µ���")]
        [Range(0, 50)] public float trimStrength = 10;
        [Tooltip("��ƽǿ��ƫ�ã����յ�VelLift =trimStrength *x +  trimBias")]
        [Range(0, 50)] public float trimBias = 8;
        private float prevTrim;
        [UdonSynced] public float trim;//��ǰ��ƽλ�ã�-1~1
        public float TrimError = 0;
        public float TrimErrorIntergrate = 0;
        public float targetPitch = 0;
        

        [Header("animation")]
        public string animatorParameterName = "elevtrim";

        [Header("Haptics")]
        public Vector3 vrInputAxis = Vector3.forward;
        [Range(0, 1)] public float hapticDuration = 0.2f;
        [Range(0, 1)] public float hapticAmplitude = 0.5f;
        [Range(0, 1)] public float hapticFrequency = 0.1f;

        [Header("Debug")]
        public Transform debugControllerTransform;
        [Tooltip("�Զ���ƽĬ�Ͽ���")]
        public int autoTrim = 1; //0-�� 1-����ģʽ 2-����ģʽ 3-��ƽģʽ (��ʱֻ�����˱��1)
        public bool TrimActive = false; //�����(SFEXT_O_JoystickGrabbed/SFEXT_O_JoystickDropped)�Լ�AP(JoystickOverride)������ʱ����ƽ�ż���
        private float rotationInputLastFrame = 0;
        
            
        private void ResetStatus() {
            //�Զ���ƽĬ�Ͽ���
            autoTrim = 1;
            Dial_Funcon.SetActive(autoTrim>0);
            prevTrim = trim = 0;
            Dial_Funcon.SetActive(autoTrim >0);
            if (vehicleAnimator) vehicleAnimator.SetFloat(animatorParameterName, .5f);
            //SAVControl.SetProgramVariable("VelLiftStart", trimStrength* trim + trimBias);
            vehicleRigidbody = SAVControl.VehicleRigidbody;
            TrimError = 0;
            TrimErrorIntergrate = 0;
    }
        
        private void PilotUpdate() {
            var input = 0f;
            //2022-12-03����Զ���ƽ����

            //����Զ���ƽ�Ƿ���Ҫ���룬����Ŀ�긩��
            if (Mathf.Abs(SAVControl.RotationInputs.x) < 0.1f) 
            {
                if (Mathf.Abs(rotationInputLastFrame) > 0.1f && Mathf.Abs(BasicFlightData.verticalG - 1) < 1f) {
                    targetPitch = BasicFlightData.pitch;
                    TrimError = 0;
                    TrimErrorIntergrate = 0;
                    TrimActive = true;
                }

            }
            else {
                TrimActive = false;
                trim = 0;
            }
            rotationInputLastFrame = SAVControl.RotationInputs.x;

            //������ƽֵ
                //����ģʽ
                if (TrimActive && autoTrim == 1 && !SAVControl.Taxiing)
                {
                    TrimError = (targetPitch - BasicFlightData.pitch);

                //ʹ���غɿ�����
                var kp = 1.3f;
                var ki = 0.015f;
                //TrimError = (targetPitch - BasicFlightData.pitch);
                TrimError = (1f - BasicFlightData.verticalG);
                TrimErrorIntergrate += TrimError;
                trim = Mathf.Lerp(trim, Mathf.Clamp(kp * TrimError + ki * TrimErrorIntergrate, -1, 1), 0.1f);

                ////ʹ�ø����ǿ�����
                //var kp = 0.02f;
                //var ki = 0.01f;
                //TrimError = (targetPitch - BasicFlightData.pitch);
                //TrimErrorIntergrate += TrimError; //�ѻ��������ŵ���Ӧ����
                //trim = Mathf.Lerp(trim, Mathf.Clamp(kp * TrimError + ki * TrimErrorIntergrate, -1, 1), 0.1f);


            }
                //����ģʽ
                else if (TrimActive && autoTrim > 0 && SAVControl.Taxiing)
                {
                    trim = Mathf.Lerp(trim, 0.4f, 0.002f); ;//�Ȱ���ƽ������һ��������ɵ�λ��(����������һλ�ã�����ӵ�˲�䴥��)
                }
                //��ƽģʽ(���Ӹ�״̬)
                else if ( autoTrim > 0 && radioAltimeter.radioAltitude < 50 && !SAVControl.Taxiing && BasicFlightData.verticalSpeed<0)
                {
                    //�����ǿ�����
                    var kp = 0.02f;
                    var ki = 0.0001f;
                    targetPitch = -2f;
                    TrimError = (targetPitch - BasicFlightData.pitch);
                    TrimErrorIntergrate += TrimError;
                    trim = Mathf.Lerp(trim, Mathf.Clamp(kp * TrimError + ki * TrimErrorIntergrate, -1, 1), 0.1f);
                }

            else {//�ֶ���ƽ
                input = GetSliderInput();
                trim = Mathf.Clamp(trim + input, -1, 1);
            }

            if (!Mathf.Approximately(input, 0) &&
                Time.frameCount % Mathf.FloorToInt(hapticDuration / Time.fixedDeltaTime) == 0) PlayHapticEvent();
        }

        private void LocalUpdate() {
            var trimChanged = !Mathf.Approximately(trim, prevTrim);
            prevTrim = trim;
            if (trimChanged) {
                SetDirty();
                if (vehicleAnimator) vehicleAnimator.SetFloat(animatorParameterName, Remap01(trim, -1, 1));
                //SAVControl.SetProgramVariable("VelLiftStart", trim * trimStrength + trimBias);
                DebugOut.text = "TRIM[F6]\n" + (trim).ToString("f2") + (TrimActive ? "\nAuto": "\n");
            }
        }

        private void FixedUpdate() {
            if (!isOwner) return;

            var rotlift = Mathf.Clamp(BasicFlightData.TAS * 0.5144f / rotMultiMaxSpeed, -1, 1);
            //SAVControl.SetProgramVariable("VelLiftStart", trim * trimStrength + trimBias);
            vehicleRigidbody.AddForceAtPosition((trim * trimStrength + trimBias) * rotlift * SAVControl.Atmosphere * vehicleRigidbody.mass * -transform.up, transform.position, ForceMode.Force);
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
            if (autoTrim > 0)
                autoTrim = 0;
            else
                autoTrim = 1;
            //���ݷ��н׶��ж���ƽģʽ
            Dial_Funcon.SetActive(autoTrim > 0);

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
            gameObject.SetActive(autoTrim>0);
            isSelected = false;
            triggerTapTime = 1;
        }

        public void SFEXT_L_EntityStart() {
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
            return sliderInput;
        }

    #endregion
    }
}