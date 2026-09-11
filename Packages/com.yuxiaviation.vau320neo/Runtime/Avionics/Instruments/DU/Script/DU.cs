using System;
using A320VAU.Common;
using UdonSharp;
using UnityEngine;
using Random = UnityEngine.Random;

namespace A320VAU.PFD {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DU : UdonSharpBehaviour {
        private DependenciesInjector _injector;
        private SystemEventBus _eventBus;
        

        public bool inSelfTest;
        public bool isSelfTestCompleted;

        public bool byPassSelfTest;
        public Animator displayUnitAnimator;

        public void Start() {
            _injector = DependenciesInjector.GetInstance(this);
            _eventBus = _injector.systemEventBus;
            _eventBus.RegisterSaccEvent(this);
            displayUnitAnimator.keepAnimatorStateOnDisable = true;
        }

        // Sacc Event
        public void SFEXT_G_Explode() => InitDU();
        public void SFEXT_G_RespawnButton() => InitDU();

        public void SFEXT_O_PilotExit() {
        }
        public void SFEXT_P_PassengerExit() {

        }

        private void OnEnable() {//上电的话对象就会被激活
            if (isSelfTestCompleted) {
                displayUnitAnimator.enabled = false;
            }
            else {
                displayUnitAnimator.enabled = true;
                inSelfTest = true;
                StartSelftest();
            }
        }

        private void OnDisable() {
            //先不实现这个功能，避免时切换座位等原因导致错误触发
        }

        public void StartSelftest() {
            if (isSelfTestCompleted | !inSelfTest | byPassSelfTest | displayUnitAnimator.GetCurrentAnimatorStateInfo(1).IsName("self test")) {
                return;
            }
            else {
                displayUnitAnimator.SetTrigger("SelfTest");
                SendCustomEventDelayedSeconds(nameof(SelfTestDone), 10);
                Debug.Log("DU Start Selftest");
            }

            
        }

        public void SelfTestDone() {
            if (displayUnitAnimator.GetCurrentAnimatorStateInfo(1).IsName("default")) {
                displayUnitAnimator.enabled = false;
                inSelfTest = false;
                isSelfTestCompleted = true;
            }
            else {
                SendCustomEventDelayedSeconds(nameof(SelfTestDone), 10);
                Debug.Log("DU Selftest over time, ");
            }
        }

        public void BypassSelftest() {
            InitDU();
            Debug.Log("DU Bypass self-test");
            inSelfTest = false;
            isSelfTestCompleted = true;
        }

        public void InitDU() {
            Debug.Log("DU Init");
            displayUnitAnimator.enabled = true;
            inSelfTest = false;
            isSelfTestCompleted = false;
        }
    }
}