﻿using System;
using A320VAU.Common;
using A320VAU.SFEXT;
using Avionics.Systems.Common;
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace A320VAU {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DFUNC_a320_AutoThrust : UdonSharpBehaviour {
        public SFEXT_a320_AdvancedEngine[] engines = { };

        private DependenciesInjector _injector;
        private AircraftSystemData _aircraftSystemData;
        private SaccAirVehicle _saccAirVehicle;
        private SaccEntity _saccEntity;

        private VRCPlayerApi localPlayer;

        [Tooltip("Object enabled when function is active (used on MFD)")]
        public GameObject Dial_Funcon;

        private bool UseLeftTrigger;
        private bool TriggerLastFrame;
        private float TriggerTapTime = 0;

        public float CruiseProportional = .1f;
        public float CruiseIntegral = .1f;
        public float CruiseIntegrator;
        public float CruiseIntegratorMax = 5;
        public float CruiseIntegratorMin = -5;
        private float Cruiselastframeerror;

        private float CruiseTemp;
        private float SpeedZeroPoint;
        [NonSerialized] public float SetSpeed;

        [NonSerialized] public bool Cruise;
        private bool func_active;
        private bool Selected;
        private bool Piloting;

        private bool EngineOn => IsEngineOn();
        private bool InReverse => IsReverse();
        private bool InVR;

        private Transform ControlsRoot;

        public void SFEXT_L_EntityStart() {

            _injector = DependenciesInjector.GetInstance(this);
            _aircraftSystemData = _injector.equipmentData;
            _saccAirVehicle = _injector.saccAirVehicle;
            _saccEntity = _injector.saccEntity;

            localPlayer = Networking.LocalPlayer;
            if (localPlayer != null) {
                InVR = localPlayer.IsUserInVR();
            }

            ControlsRoot = _saccAirVehicle.ControlsRoot;
            if (Dial_Funcon) Dial_Funcon.SetActive(false);
        }

        private bool IsReverse() {
            foreach (var engine in engines) {
                if (engine.reversing) return true;
            }

            return false;
        }

        private bool IsEngineOn() {
            foreach (var engine in engines) {
                if (engine.fuel) return true;
            }

            return false;
        }

        public void DFUNC_LeftDial() {
            UseLeftTrigger = true;
        }

        public void DFUNC_RightDial() {
            UseLeftTrigger = false;
        }

        public void DFUNC_Selected() {
            TriggerLastFrame = true;
            gameObject.SetActive(true);
            Selected = true;
        }

        public void DFUNC_Deselected() {
            if (!Cruise) {
                gameObject.SetActive(false);
            }

            Selected = false;
        }

        public void SFEXT_O_PilotEnter() {
            if (Dial_Funcon) Dial_Funcon.SetActive(Cruise);
            Piloting = true;
        }

        public void SFEXT_P_PassengerEnter() {
            if (Dial_Funcon) Dial_Funcon.SetActive(Cruise);
        }

        public void SFEXT_O_PilotExit() {
            Piloting = false;
            Selected = false;
        }

        public void SFEXT_G_Explode() => SetCruiseOff();
        public void SFEXT_G_TouchDown() => SetCruiseOff();

        private void LateUpdate() {
            if (_aircraftSystemData.throttleLevelerSlot == ThrottleLevelerSlot.TOGA ||
                _aircraftSystemData.throttleLevelerSlot == ThrottleLevelerSlot.IDLE ||
                _aircraftSystemData.throttleLevelerSlot == ThrottleLevelerSlot.FlexMct) {

                SetCruiseOff();
                return;
            }

            if (!EngineOn) {
                SetCruiseOff();
                return;
            }

            if (InVR) {
                if (Selected) {
                    float Trigger;
                    if (UseLeftTrigger) {
                        Trigger = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryIndexTrigger");
                    }
                    else {
                        Trigger = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger");
                    }

                    if (Trigger > 0.75) {
                        //for setting speed in VR
                        Vector3 handpos = ControlsRoot.position -
                                          localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand)
                                              .position;
                        handpos = ControlsRoot.InverseTransformDirection(handpos);

                        //enable and disable
                        if (!TriggerLastFrame) {
                            if (!Cruise) {
                                if (!_saccAirVehicle.Taxiing && !InReverse) {
                                    SetCruiseOn();
                                }
                            }

                            if (Time.time - TriggerTapTime < .4f) //double tap detected, turn off cruise
                            {
                                SetCruiseOff();
                            }

                            SpeedZeroPoint = handpos.z;
                            CruiseTemp = SetSpeed;
                            TriggerTapTime = Time.time;
                        }

                        float SpeedDifference = (SpeedZeroPoint - handpos.z) * 250;
                        SetSpeed = Mathf.Clamp(CruiseTemp + SpeedDifference, 0, 2000);

                        TriggerLastFrame = true;
                    }
                    else {
                        TriggerLastFrame = false;
                    }
                }
            }

            float DeltaTime = Time.deltaTime;
            float equals = Input.GetKey(KeyCode.Equals) ? DeltaTime * 10 : 0;
            float minus = Input.GetKey(KeyCode.Minus) ? DeltaTime * 10 : 0;
            SetSpeed = Mathf.Max(SetSpeed + (equals - minus), 0);

            if (func_active) {
                float error = SetSpeed - _saccAirVehicle.AirSpeed;

                CruiseIntegrator += error * DeltaTime;
                CruiseIntegrator = Mathf.Clamp(CruiseIntegrator, CruiseIntegratorMin, CruiseIntegratorMax);

                foreach (var engine in engines) {
                    engine.autoThrustInput =
                        Mathf.Clamp((CruiseProportional * error) + (CruiseIntegral * CruiseIntegrator), 0, 1);
                }
            }
        }

        public void KeyboardInput() {
            if (!Cruise) {
                if (!_saccAirVehicle.Taxiing && !InReverse) {
                    SetCruiseOn();
                }
            }
            else {
                SetCruiseOff();
            }
        }

        public void SetCruiseOn() {
            if (Cruise) {
                return;
            }

            if (Piloting) {
                func_active = true;
                if (!InVR) {
                    gameObject.SetActive(true);
                }
            }

            foreach (var engine in engines) {
                engine.isAutoThrustActive = true;
            }

            SetSpeed = _saccAirVehicle.AirSpeed;
            Cruise = true;
            if (Dial_Funcon) {
                Dial_Funcon.SetActive(true);
            }

            _saccEntity.SendEventToExtensions("SFEXT_O_CruiseEnabled");
        }

        public void SetCruiseOff() {
            if (!Cruise) {
                return;
            }

            if (Piloting) {
                func_active = false;
                if (!InVR) {
                    gameObject.SetActive(false);
                }
            }

            foreach (var engine in engines) {
                engine.isAutoThrustActive = false;
            }

            Cruise = false;
            if (Dial_Funcon) {
                Dial_Funcon.SetActive(false);
            }

            _saccEntity.SendEventToExtensions("SFEXT_O_CruiseDisabled");
        }

        // public void SFEXT_O_ThrottleDropped() {
        //     if (!CruiseThrottleOverridden && Cruise) {
        //         SAVControl.SetProgramVariable("ThrottleOverridden",
        //             (int)SAVControl.GetProgramVariable("ThrottleOverridden") + 1);
        //         CruiseThrottleOverridden = true;
        //     }
        // }
        //
        // public void SFEXT_O_ThrottleGrabbed() {
        //     if (CruiseThrottleOverridden) {
        //         SAVControl.SetProgramVariable("ThrottleOverridden",
        //             (int)SAVControl.GetProgramVariable("ThrottleOverridden") - 1);
        //         CruiseThrottleOverridden = false;
        //     }
        // }

        public void SFEXT_O_LoseOwnership() {
            gameObject.SetActive(false);
            func_active = false;
            if (Cruise) {
                SetCruiseOff();
            }
        }
    }
}