using A320VAU.SFEXT;
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;

namespace A320VAU.DFUNC {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DFUNC_a320_AdvancedThrustReverser : UdonSharpBehaviour {
        public KeyCode keyboardControl = KeyCode.R;
        public GameObject Dial_Funcon;
        private SFEXT_a320_AdvancedEngine[] engines;
        private bool selected, isPilot;
        private string triggerAxis;

        private void Update() {
            if (isPilot) {
                var trigger = GetInput();
                foreach (var engine in engines) {
                    if (!engine) continue;
                    var reversing = engine.reversing;
                    if (trigger && !reversing && Mathf.Approximately(engine.throttleInput, 0)) engine.reversing = true;
                    else if (!trigger && reversing) engine.reversing = false;
                }

                if (Dial_Funcon) Dial_Funcon.SetActive(trigger);
            }
        }

        public void SFEXT_L_EntityStart() {
            var entity = GetComponentInParent<SaccEntity>();
            engines = entity.gameObject.GetComponentsInChildren<SFEXT_a320_AdvancedEngine>(true);

            if (Dial_Funcon) Dial_Funcon.SetActive(false);

            gameObject.SetActive(false);
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
            isPilot = true;
            gameObject.SetActive(true);
        }

        public void SFEXT_O_PilotExit() {
            isPilot = false;
            selected = false;
            gameObject.SetActive(false);
        }

        private bool GetInput() {
            return Input.GetKey(keyboardControl) || (selected && Input.GetAxisRaw(triggerAxis) > 0.75f);
        }
    }
}