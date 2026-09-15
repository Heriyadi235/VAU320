using System;
using A320VAU.SFEXT;
using EsnyaSFAddons.SFEXT;
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using YuxiFlightInstruments.ElectricalBus;

//note:this code is original from https://github.com/esnya/EsnyaSFAddons
//to satisfy vau320's demand, add eletrical start
namespace A320VAU.DFUNC {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DefaultExecutionOrder(1000)] // After SaccAirVehicle
    public class DFUNC_a320_AutoStarter : UdonSharpBehaviour {
        public const byte STATE_OFF = 0;
        public const byte STATE_ElETRICAL_START = 1;
        public const byte STATE_ElETRICAL_STOP = 2;
        public const byte STATE_APU_START = 3;
        public const byte STATE_APU_STOP = 4;
        public const byte STATE_ENGINE_START = 5;
        public const byte STATE_ENGINE_STOP = 6;
        public const byte STATE_ON = 255;

        public KeyCode startKey = KeyCode.LeftShift;
        public KeyCode stopKey = KeyCode.RightControl;
        public GameObject Dial_Funcon;
        public bool desktopOnly;

        [Header("Engine")]
        [Tooltip("[s]")] public float engineStartInterval = 30.0f;

        [Tooltip("[s]")] public float engineStopInterval = 30.0f;
        private SaccAirVehicle airVehicle;
        private SFEXT_AuxiliaryPowerUnit apu;
        private YFI_ElectricalBus eletricalBus;
        private SFEXT_a320_AdvancedEngine[] engines;

        private bool holdThrottle;
        private bool initialized, selected, isPilot, isPassenger, isOwner, prevTrigger;

        private byte prevState;

        [Header("Runtime Synced State")]
        [NonSerialized] public bool start;

        [Header("Runtime Local State")]
        [NonSerialized] [UdonSynced] public byte state;

        private float stateChangedTime;
        private string triggerAxis;

        private void Update() {
            if (!initialized) return;

            var time = Time.time;

            if (isPilot) {
                if (Input.GetKeyDown(startKey)) {
                    if (!start) {
                        SetStart(true);
                        holdThrottle = true;
                    }
                }
                else if (Input.GetKeyDown(stopKey)) {
                    SetStart(false);
                }

                if (holdThrottle && Input.GetKeyUp(startKey)) {
                    holdThrottle = false;
                    airVehicle.ThrottleInput = 0.375f;
                }

                var trigger = selected && Input.GetAxisRaw(triggerAxis) > 0.75f;
                if (!prevTrigger && trigger) SetStart(!start);
                prevTrigger = trigger;
            }

            if (isOwner) {
                if (Dial_Funcon && Dial_Funcon.activeSelf != start) Dial_Funcon.SetActive(start);
            }
            else if (isPassenger) {
                var remoteStart = state != STATE_OFF;
                if (Dial_Funcon && Dial_Funcon.activeSelf != remoteStart) Dial_Funcon.SetActive(remoteStart);
            }

            var stateChanged = state != prevState;
            prevState = state;

            if (stateChanged) {
                stateChangedTime = time;
                switch (state) {
                    case STATE_OFF:
                        Debug.Log("[ZHI][AutoStarter] Off");
                        break;
                    case STATE_ElETRICAL_START:
                        Debug.Log("[ZHI][AutoStarter] Eletrical Start");
                        break;
                    case STATE_ElETRICAL_STOP:
                        Debug.Log("[ZHI][AutoStarter] Eletrical Stop");
                        break;
                    case STATE_APU_START:
                        Debug.Log("[ZHI][AutoStarter] APU Start");
                        break;
                    case STATE_APU_STOP:
                        Debug.Log("[ZHI][AutoStarter] APU Stop");
                        break;
                    case STATE_ENGINE_START:
                        Debug.Log("[ZHI][AutoStarter] Engine Start");
                        break;
                    case STATE_ENGINE_STOP:
                        Debug.Log("[ZHI][AutoStarter] Engine Stop");
                        break;
                    case STATE_ON:
                        Debug.Log("[ZHI][AutoStarter] On");
                        break;
                }
            }

            var stateTime = time - stateChangedTime;

            switch (state) {
                case STATE_OFF:
                    if (start) SetState(STATE_ElETRICAL_START);
                    break;
                case STATE_ElETRICAL_START:
                    if (isOwner) {
                        if (stateChanged && !eletricalBus.batteryOn) eletricalBus.OnToggleBattery();
                        if (!eletricalBus || eletricalBus.hasPower) SetState(STATE_APU_START);
                    }

                    break;
                case STATE_ElETRICAL_STOP:
                    if (isOwner) {
                        if (stateChanged && eletricalBus.batteryOn) eletricalBus.OnToggleBattery();
                        if (!eletricalBus || !eletricalBus.hasPower) SetState(start ? STATE_ON : STATE_OFF);
                    }

                    break;
                case STATE_APU_START:
                    if (isOwner) {
                        if (stateChanged && apu) apu.StartAPU();
                        if (!apu || apu.started) SetState(STATE_ENGINE_START);
                    }

                    break;
                case STATE_APU_STOP:
                    if (isOwner) {
                        if (stateChanged && apu) apu.StopAPU();
                        if (!apu || apu.terminated) SetState(start ? STATE_ON : STATE_OFF);
                    }

                    break;
                case STATE_ENGINE_START:
                    if (isOwner) {
                        var starterIndex = engines.Length - Mathf.FloorToInt(stateTime / engineStartInterval) - 1;
                        if (starterIndex >= 0 && starterIndex < engines.Length) engines[starterIndex].starter = true;
                    }

                    var allEngineStarted = true;
                    foreach (var engine in engines) {
                        if (!engine) continue;
                        if (engine.n2 >= engine.minN2)
                            if (isOwner)
                                engine.fuel = true;

                        if (engine.n1 >= engine.idleN1 * 0.9f) {
                            if (isOwner) engine.starter = false;
                        }
                        else {
                            allEngineStarted = false;
                        }
                    }

                    if (allEngineStarted) SetState(STATE_APU_STOP);
                    break;
                case STATE_ENGINE_STOP:
                    var index = engines.Length - Mathf.FloorToInt(stateTime / engineStopInterval) - 1;
                    if (index < 0) {
                        SetState(STATE_ElETRICAL_STOP);
                    }
                    else if (index < engines.Length && isOwner) {
                        engines[index].starter = false;
                        engines[index].fuel = false;
                    }

                    break;

                case STATE_ON:
                    if (!start) SetState(STATE_ENGINE_STOP);
                    break;
            }

            if (isOwner && !isPilot && (state == STATE_ON || state == STATE_OFF)) gameObject.SetActive(false);
        }

        public void SFEXT_L_EntityStart() {
            var entity = GetComponentInParent<SaccEntity>();
            airVehicle = (SaccAirVehicle)GetExtention(entity, GetUdonTypeName<SaccAirVehicle>());

            eletricalBus = (YFI_ElectricalBus)GetExtention(entity, GetUdonTypeName<YFI_ElectricalBus>());
            apu = (SFEXT_AuxiliaryPowerUnit)GetExtention(entity, GetUdonTypeName<SFEXT_AuxiliaryPowerUnit>());
            engines = (SFEXT_a320_AdvancedEngine[])GetExtentions(entity, GetUdonTypeName<SFEXT_a320_AdvancedEngine>());

            start = false;
            state = STATE_OFF;

            if (Dial_Funcon) Dial_Funcon.SetActive(false);
            gameObject.SetActive(false);
            initialized = true;
        }

        public void DFUNC_LeftDial() {
            triggerAxis = "Oculus_CrossPlatform_PrimaryIndexTrigger";
        }

        public void DFUNC_RightDial() {
            triggerAxis = "Oculus_CrossPlatform_SecondaryIndexTrigger";
        }

        public void DFUNC_Selected() {
            selected = true;
        }

        public void DFUNC_Deselected() {
            selected = false;
        }

        public void SFEXT_O_PilotEnter() {
            if (desktopOnly && Networking.LocalPlayer.IsUserInVR()) return;

            isPilot = true;
            isOwner = true;
            selected = false;
            prevTrigger = true;
            gameObject.SetActive(true);
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

        public void SFEXT_G_Explode() {
            ResetStatus();
        }

        public void SFEXT_G_RespawnButton() {
            ResetStatus();
        }

        public void SFEXT_O_TakeOwnership() {
            isOwner = true;
        }

        public void SFEXT_O_LoseOwnership() {
            isOwner = false;
        }

        private void ResetStatus() {
            state = STATE_OFF;
            start = false;
        }

        public override void PostLateUpdate() {
            if (isOwner && holdThrottle) airVehicle.ThrottleInput = 0;
        }

        private void SetStart(bool value) {
            start = value;
        }

        private void SetState(byte value) {
            if (!isOwner) return;
            state = value;
            RequestSerialization();
        }

    #region SFEXT Utilities

        private UdonSharpBehaviour GetExtention(SaccEntity entity, string udonTypeName) {
            foreach (var extention in entity.ExtensionUdonBehaviours)
                if (extention && extention.GetUdonTypeName() == udonTypeName)
                    return extention;
            foreach (var extention in entity.Dial_Functions_L)
                if (extention && extention.GetUdonTypeName() == udonTypeName)
                    return extention;
            foreach (var extention in entity.Dial_Functions_R)
                if (extention && extention.GetUdonTypeName() == udonTypeName)
                    return extention;
            return null;
        }

        private UdonSharpBehaviour[] GetExtentions(SaccEntity entity, string udonTypeName) {
            var result = new UdonSharpBehaviour[entity.ExtensionUdonBehaviours.Length + entity.Dial_Functions_L.Length +
                                                entity.Dial_Functions_R.Length];
            var count = 0;
            foreach (var extention in entity.ExtensionUdonBehaviours)
                if (extention && extention.GetUdonTypeName() == udonTypeName)
                    result[count++] = extention;
            foreach (var extention in entity.Dial_Functions_L)
                if (extention && extention.GetUdonTypeName() == udonTypeName)
                    result[count++] = extention;
            foreach (var extention in entity.Dial_Functions_R)
                if (extention && extention.GetUdonTypeName() == udonTypeName)
                    result[count++] = extention;

            var finalResult = new UdonSharpBehaviour[count];
            Array.Copy(result, finalResult, count);

            return finalResult;
        }

    #endregion
    }
}