using System;
using A320VAU.ADIRU;
using A320VAU.Common;
using Avionics.Systems.Common;
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using YuxiFlightInstruments.BasicFlightData;

namespace A320VAU.Brake {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DefaultExecutionOrder(2010)]//after AircraftSystemData
    public class DFUNC_a320_Brake : UdonSharpBehaviour {
        /*320专用的刹车，增加了地面摩擦阻力,停留刹车*/
        public YFI_FlightDataInterface basicFilghtData;
        public AircraftSystemData aircraftSystemData;
        
        public float autoBrakeInput;
        public bool isManuelBrakeInUse { get; private set; }

        [Tooltip("Looping sound to play while brake is active")]
        public AudioSource Airbrake_snd;

        [Tooltip("Will Crash if not set")]
        public Animator BrakeAnimator;

        [Tooltip(
            "Because you have to hold the break, and the keyboardcontrols script can only send events, this option is here.")]
        public KeyCode KeyboardControl = KeyCode.B;

        public KeyCode ParkBreakControl = KeyCode.P;
        [UdonSynced] public bool ParkBreakSet = true;
        public float AirbrakeStrength = 4f;

        public bool NoPilotAlwaysParkBrake = true;

        [Tooltip("停留刹车指示")]
        public GameObject Dial_Funcon;


        
        private float AirbrakeLerper;
        private int BRAKE_STRING = Animator.StringToHash("brake");
        [NonSerialized] [UdonSynced] public float BrakeInput;
        [NonSerialized] public bool _DisableGroundBrake;
        private float BrakeStrength;

        private bool Braking;
        private bool BrakingLastFrame;

        private SaccEntity EntityControl;
        public SaccAirVehicle SAVControl;
        private bool HasAirBrake;
        private bool IsOwner;
        private float LastDrag;
        private float NextUpdateTime;

        private float NonLocalActiveDelay; //this var is for adding a min delay for disabling for non-local users to account for lag

        private bool prevKeyPress;
        private bool prevTriggered;
        private float RotMultiMaxSpeedDivider;
        private bool Selected;
        private float triggerTapTime = 1;

        private bool UseLeftTrigger;
        private Rigidbody VehicleRigidbody;
        private ADIRU.ADIRU _adiru;

        private void Update() {
            var DeltaTime = Time.deltaTime;
            if (IsOwner) {
                triggerTapTime += Time.deltaTime;

                var Taxiing = SAVControl.Taxiing;
                if (SAVControl.Piloting) {
                    float KeyboardBrakeInput = 0;
                    float VRBrakeInput = 0;

                    if (Selected) {
                        float Trigger;
                        if (UseLeftTrigger)
                            Trigger = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryIndexTrigger");
                        else
                            Trigger = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger");
                        VRBrakeInput = Trigger;
                        if (Trigger > 0.75f) {
                            if (!prevTriggered) {
                                if (triggerTapTime > .4f) //no double tap
                                {
                                    triggerTapTime = 0;
                                }
                                else //double tap detected, switch break
                                {
                                    ToggleParkBrake();
                                    triggerTapTime = 1;
                                }
                            }

                            prevTriggered = true;
                        }
                        else {
                            prevTriggered = false;
                        }
                        //VR双击启动地面刹车
                    } //获取轴输入

                    if (Input.GetKey(KeyboardControl)) KeyboardBrakeInput = 1; //获取键盘输入
                    if (Input.GetKey(ParkBreakControl)) {
                        if (!prevKeyPress) {
                            ToggleParkBrake();
                            prevKeyPress = true;
                        }
                    } //生成parkbreak开关或brakeinput
                    else {
                        prevKeyPress = false;
                    }

                    BrakeInput = Mathf.Max(VRBrakeInput, KeyboardBrakeInput);
                    isManuelBrakeInUse = BrakeInput > 0;

                    if (BrakeInput < autoBrakeInput) {
                        BrakeInput = autoBrakeInput;
                    }

                    if (!HasAirBrake && !Taxiing) BrakeInput = 0;
                    //remove the drag added last frame to add the new value for this frame
                    var extradrag = SAVControl.ExtraDrag;
                    var newdrag = AirbrakeStrength * BrakeInput;
                    var dragtoadd = -LastDrag + newdrag;
                    extradrag += dragtoadd;
                    LastDrag = newdrag;
                    SAVControl.ExtraDrag = extradrag;

                    //send events to other users to tell them to enable the script so they can see the animation
                    Braking = BrakeInput > .02f;
                    if (Braking) {
                        if (!BrakingLastFrame) {
                            if (Airbrake_snd && !Airbrake_snd.isPlaying) Airbrake_snd.Play();
                            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(EnableForAnimation));
                        }

                        if (Time.time > NextUpdateTime) {
                            RequestSerialization();
                            NextUpdateTime = Time.time + .4f;
                        }
                    }
                    else {
                        if (BrakingLastFrame) {
                            var brk = BrakeInput;
                            BrakeInput = 0;
                            RequestSerialization();
                            BrakeInput = brk;
                        }
                    }

                    if (AirbrakeLerper < .03 && BrakeInput < .03)
                        if (Airbrake_snd && Airbrake_snd.isPlaying)
                            Airbrake_snd.Stop();
                    BrakingLastFrame = Braking;

                }
            }  
            else {
                //this object is enabled for non-owners only while animating
                NonLocalActiveDelay -= DeltaTime;
                if (NonLocalActiveDelay < 0 && AirbrakeLerper < 0.01) {
                    DisableForAnimation();
                    return;
                }
            }

            AirbrakeLerper = Mathf.Lerp(AirbrakeLerper, BrakeInput, 2f * DeltaTime);
            BrakeAnimator.SetFloat(BRAKE_STRING, AirbrakeLerper);
            if (Airbrake_snd) {
                Airbrake_snd.pitch = AirbrakeLerper * .2f + .9f;
                Airbrake_snd.volume = AirbrakeLerper *
                                      Mathf.Min((float)SAVControl.GetProgramVariable("Speed") * RotMultiMaxSpeedDivider,
                                          1);
            }
        }

        public override void OnDeserialization() {
            Dial_Funcon.SetActive(ParkBreakSet);
        }
        public void DFUNC_LeftDial() {
            UseLeftTrigger = true;
        }

        public void DFUNC_RightDial() {
            UseLeftTrigger = false;
        }

        public void SFEXT_L_EntityStart() {

            SAVControl = basicFilghtData.SAVControl;
            _adiru = DependenciesInjector.GetInstance(this).adiru;
            VehicleRigidbody = SAVControl.VehicleRigidbody;
            HasAirBrake = AirbrakeStrength != 0;
            RotMultiMaxSpeedDivider = 1 / (float)SAVControl.GetProgramVariable("RotMultiMaxSpeed");
            IsOwner = SAVControl.IsOwner;
            var localPlayer = Networking.LocalPlayer;
            if (localPlayer != null && !localPlayer.isMaster)
                gameObject.SetActive(false);
            else
                gameObject.SetActive(true);
            Dial_Funcon.SetActive(ParkBreakSet);
        }

        public void DFUNC_Selected() {
            Selected = true;
            prevTriggered = false;
            prevKeyPress = false;
        }

        public void DFUNC_Deselected() {
            BrakeInput = 0;
            Selected = false;
        }

        public void SFEXT_O_PilotEnter() {
            prevTriggered = false;
            prevKeyPress = false;
            Dial_Funcon.SetActive(ParkBreakSet);
            RequestSerialization();
        }

        public void SFEXT_O_PilotExit() {
            BrakeInput = 0;
            Selected = false;
            if (NoPilotAlwaysParkBrake) ParkBreakSet = true;
            Dial_Funcon.SetActive(ParkBreakSet);
            RequestSerialization();
        }

        public void SFEXT_G_Explode() {
            BrakeInput = 0;
            BrakeAnimator.SetFloat(BRAKE_STRING, 0);
        }

        public void SFEXT_O_TakeOwnership() {
            gameObject.SetActive(true);
            IsOwner = true;
        }

        public void SFEXT_O_LoseOwnership() {
            gameObject.SetActive(false);
            IsOwner = false;
        }

        public void EnableForAnimation() {
            if (!IsOwner) {
                if (Airbrake_snd) Airbrake_snd.Play();
                gameObject.SetActive(true);
                NonLocalActiveDelay = 3;
            }
        }

        public void DisableForAnimation() {
            BrakeAnimator.SetFloat(BRAKE_STRING, 0);
            BrakeInput = 0;
            AirbrakeLerper = 0;
            if (Airbrake_snd) {
                Airbrake_snd.pitch = 0;
                Airbrake_snd.volume = 0;
            }

            gameObject.SetActive(false);
        }

        public void SFEXT_G_TouchDownWater() {
        }

        public void SFEXT_G_TouchDown() {
        }

        private void ToggleParkBrake() {
            ParkBreakSet = !ParkBreakSet;
            Dial_Funcon.SetActive(ParkBreakSet);
            if (IsOwner) RequestSerialization();
        }
    }
}