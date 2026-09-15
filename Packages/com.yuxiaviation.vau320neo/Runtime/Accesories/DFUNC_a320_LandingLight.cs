using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace A320VAU.DFUNC {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class DFUNC_a320_LandingLight : UdonSharpBehaviour {
        public GameObject lightObj;
        public GameObject Dial_Funcon;

        private bool state, useLeftTrigger, selected;
        //public KeyCode desktopKey = KeyCode.F5;

        private bool triggerLastFrame;

        private void Update() {
            if (GetTrigger()) {
                if (!triggerLastFrame)
                    SendCustomNetworkEvent(NetworkEventTarget.All, !state ? nameof(TurnOn) : nameof(TurnOff));

                triggerLastFrame = true;
            }
            else {
                triggerLastFrame = false;
            }
        }

        private void OnEnable() {
            triggerLastFrame = true;
        }

        private void OnDisable() {
            selected = false;
        }

        public void SFEXT_L_EntityStart() {
            var entity = GetComponentInParent<SaccEntity>();
            lightObj.transform.parent = entity.transform;
            TurnOff();
        }

        public void SFEXT_O_OnPlayerJoined() {
            SendCustomNetworkEvent(NetworkEventTarget.All, state ? nameof(TurnOn) : nameof(TurnOff));
        }

        public void SFEXT_O_PilotExit() {
            gameObject.SetActive(false);
            //SendCustomNetworkEvent(NetworkEventTarget.All, nameof(TurnOff));
        }

        public void SFEXT_O_PilotEnter() {
            gameObject.SetActive(true);
        }

        public void SFEXT_G_Explode() {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(TurnOff));
        }

        public void SFEXT_G_RespawnButton() {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(TurnOff));
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player) {
            if (player.isLocal) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(TurnOff));
        }

        public void DFUNC_Selected() {
            selected = true;
            gameObject.SetActive(true);
        }

        public void DFUNC_Deselected() {
            selected = false;
            gameObject.SetActive(false);
        }

        public void DFUNC_LeftDial() {
            useLeftTrigger = true;
        }

        public void DFUNC_RightDial() {
            useLeftTrigger = false;
        }

        public void KeyboardInput() {
            SendCustomNetworkEvent(NetworkEventTarget.All, !state ? nameof(TurnOn) : nameof(TurnOff));
        }

        private bool GetTrigger() {
            if (!selected) return false;

            return Input.GetAxisRaw(useLeftTrigger
                ? "Oculus_CrossPlatform_PrimaryIndexTrigger"
                : "Oculus_CrossPlatform_SecondaryIndexTrigger") > .75f;
        }

        private void SetState(bool value) {
            lightObj.SetActive(value);
            if (Dial_Funcon) Dial_Funcon.SetActive(value);
            state = value;
        }

        public void TurnOn() {
            SetState(true);
        }

        public void TurnOff() {
            SetState(false);
        }
    }
}