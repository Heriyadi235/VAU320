
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using SaccFlightAndVehicles;
using A320VAU.ADIRU;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace A320VAU.DFUNC {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class DFUNC_a320_FlapController : UdonSharpBehaviour {
        [Header("Specs")]
        public int flapLeverDetent = 5; 

        public float[] flapDetents = {
            0,//0
            0,//1
            10,//1+f
            15,//2
            20,//3
            35,//full
        };
        public float[] slatDetents = {
            0,
            18,
            18,
            22,
            22,
            27
        };
        [Tooltip("KIAS")]
        public float[] speedLimits = {
            350,
            230,
            215,
            200,
            185,
            177
        };
        [Tooltip("Drag")]
        public float[] dragMultiplier = {
            1,
            1.4f,
            1.6f,
            1.8f,
            1.9f,
            2f
        };

        public float[] liftMultiplier = {
            1,
            1.456f,
            1.6f,
            1.84f,
            1.9f,
            2.06f
        };

        public float response = 1f;
        public GameObject powerSource;
        [Header("Flight State")]
        public ADIRU.ADIRU adiru;

        [Header("Inputs")]
        public float controllerSensitivity = 10f;
        public Vector3 vrInputAxis = Vector3.forward;
        public KeyCode desktopKey = KeyCode.F;
        public bool seamless = true;

        [Header("Animator")]
        public string boolParameterName = "flaps";
        public string flapAngleParameterName = "flapsangle", slatAngleParameterName = "slatsangle", flapLeverParameterName = "flapslever", brokenParameterName = "flapsbroken";

        [Header("Sounds")]
        public AudioSource[] audioSources = { };
        public float soundResponse = 1;
        public AudioSource[] breakingSounds = { };

        [Header("Faults")]
        public float meanTimeBetweenActuatorBrokenOnOverspeed = 120.0f;
        public float meanTimeBetweenWingBrokenOnOverspeed = 240.0f;
        public float overspeedDamageMultiplier = 10.0f;
        public float brokenDragMultiplier = 2.9f;
        public float brokenLiftMultiplier = 0.3f;

        [Header("Haptics")]
        [Range(0, 1)] public float hapticDuration = 0.1f;
        [Range(0, 1)] public float hapticAmplitude = 0.5f;
        [Range(0, 1)] public float hapticFrequency = 0.1f;

        /*[System.NonSerialized]*/ [UdonSynced(UdonSyncMode.None)] public int leverIndex;
        /*[HideInInspector]*/
        public int detentIndex, targetDetentIndex; //detentIndex为 负n 表示襟翼向着n+1位置移动中
        public float targetFlapAngle, targetSlatAngle, targetLift, targetDrag, targetSpeedLimit;
        public float flapAngle, slatAngle, speedLimit, lift, drag;
        public float maxFlapAngle, maxSlatAngle;
        private Animator vehicleAnimator;
        
        [UdonSynced] bool actuatorBroken;
        [UdonSynced][FieldChangeCallback(nameof(WingBroken))] bool _wingBroken;
        private bool WingBroken {
            set {
                if (value == _wingBroken) return;
                _wingBroken = value;

                if (vehicleAnimator) {
                    vehicleAnimator.SetBool(brokenParameterName, value);
                }

                if (value) {
                    foreach (var audioSource in breakingSounds) {
                        if (audioSource) audioSource.PlayScheduled(Random.value * 0.1f);
                    }
                }
            }
            get => _wingBroken;
        }
        private string triggerAxis;
        private VRCPlayerApi.TrackingDataType trackingTarget;
        private bool hasPilot, isPilot, isOwner, selected;
        private SaccAirVehicle airVehicle;
        private Transform controlsRoot;
        private float[] audioVolumes, audioPitches;
        public void DFUNC_LeftDial() {
            triggerAxis = "Oculus_CrossPlatform_PrimaryIndexTrigger";
            trackingTarget = VRCPlayerApi.TrackingDataType.LeftHand;
        }
        public void DFUNC_RightDial() {
            triggerAxis = "Oculus_CrossPlatform_SecondaryIndexTrigger";
            trackingTarget = VRCPlayerApi.TrackingDataType.RightHand;
        }

        public void SFEXT_L_EntityStart() {
            var entity = GetComponentInParent<SaccEntity>();
            airVehicle = (SaccAirVehicle)entity.GetExtention(GetUdonTypeName<SaccAirVehicle>());

            vehicleAnimator = airVehicle.VehicleAnimator;

            controlsRoot = airVehicle.ControlsRoot;
            if (!controlsRoot) controlsRoot = entity.transform;

            maxFlapAngle = flapDetents[flapDetents.Length - 1];
            maxSlatAngle = slatDetents[slatDetents.Length - 1];

            audioVolumes = new float[audioSources.Length];
            audioPitches = new float[audioSources.Length];
            for (var i = 0; i < audioSources.Length; i++) {
                var audioSource = audioSources[i];
                if (!audioSource) continue;

                audioVolumes[i] = audioSource.volume;
                audioPitches[i] = audioSource.pitch;
            }

            ResetStatus();
        }

        public void SFEXT_O_PilotEnter() {
            isPilot = true;
            isOwner = true;
            selected = false;
        }
        public void SFEXT_O_PilotExit() => isPilot = false;

        public void SFEXT_O_TakeOwnership() => isOwner = true;
        public void SFEXT_O_LoseOwnership() => isOwner = false;

        public void SFEXT_G_PilotEnter() {
            hasPilot = true;
            gameObject.SetActive(true);
        }
        public void SFEXT_G_PilotExit() => hasPilot = false;
        public void SFEXT_G_Explode() => ResetStatus();
        public void SFEXT_G_RespawnButton() => ResetStatus();

        public void DFUNC_Selected() => selected = true;
        public void DFUNC_Deselected() => selected = false;

        private void Update() {
            var deltaTime = Time.deltaTime;

            if (isOwner) ApplyDamage(deltaTime);

            var actuatorAvailable = !actuatorBroken && (!powerSource || powerSource.activeInHierarchy);
            UpdateSounds(deltaTime, actuatorAvailable);

            if (!Mathf.Approximately(targetFlapAngle, flapAngle) || !Mathf.Approximately(targetSlatAngle, slatAngle)) {
                if (actuatorAvailable) {
                    flapAngle = Mathf.MoveTowards(flapAngle, targetFlapAngle, response * deltaTime);
                    slatAngle = Mathf.MoveTowards(slatAngle, targetSlatAngle, response * deltaTime);
                    speedLimit = Mathf.MoveTowards(speedLimit, targetSpeedLimit, response * 50 * deltaTime);
                    lift = Mathf.MoveTowards(lift, targetLift, response * 0.1f * deltaTime);
                    drag = Mathf.MoveTowards(drag, targetDrag, response * 0.1f * deltaTime);
                    ApplyParameters();
                    if (vehicleAnimator) {
                        vehicleAnimator.SetFloat(flapAngleParameterName, flapAngle / maxFlapAngle);
                        vehicleAnimator.SetFloat(slatAngleParameterName, slatAngle / maxFlapAngle);
                    }
                }
                else {
                    if (!hasPilot) gameObject.SetActive(false);
                }
            }
            else 
            {
                detentIndex = targetDetentIndex;
            }
        }


        private void LateUpdate() {
            if (isPilot) HandleInput();
        }

        private void ResetStatus() {
            leverIndex = detentIndex = 0;
            flapAngle = slatAngle = 0;
            speedLimit = speedLimits[0];
            lift = drag = 1;
            SetMovementTarget();
            actuatorBroken = WingBroken = false;

            airVehicle.ExtraDrag -= appliedExtraDrag;
            airVehicle.ExtraLift -= appliedExtraLift;
            appliedExtraDrag = 0;
            appliedExtraLift = 0;

            gameObject.SetActive(false);
        }

        private bool prevTrigger;
        private Vector3 trackingOrigin;
        private float targetDetentOrigin;
        private void HandleInput() {
            if (selected) {
                var trigger = Input.GetAxis(triggerAxis) > 0.7f;
                var triggerChanged = prevTrigger != trigger;
                prevTrigger = trigger;

                if (trigger) {
                    var trackingPosition = controlsRoot.InverseTransformPoint(Networking.LocalPlayer.GetTrackingData(trackingTarget).position);
                    if (triggerChanged) {
                        trackingOrigin = trackingPosition;
                        targetDetentOrigin = leverIndex;
                    }
                    else {
                        leverIndex = (int)Mathf.Clamp(targetDetentOrigin - Vector3.Dot(trackingPosition - trackingOrigin, vrInputAxis) * (flapLeverDetent-1) * controllerSensitivity, 0, (flapLeverDetent-1));
                        if (isPilot && targetDetentOrigin != leverIndex) {
                            OnLeverChanged();
                            PlayHapticEvent(); 
                            
                        }
                    }
                }

                if (triggerChanged && !trigger && !seamless) {
                    OnLeverChanged();
                }
            }

            if (Input.GetKeyDown(desktopKey)) {
                leverIndex = (leverIndex + 1) % flapLeverDetent;
                OnLeverChanged();
            }
        }

        private void OnLeverChanged() {
            if(isOwner)
                RequestSerialization();
            SetMovementTarget();
        }


        public override void OnDeserialization() {
            base.OnDeserialization();
            SetMovementTarget();
        }

        public void SetMovementTarget() {
            //targetLeverIndex: 0 1     2 3 4
            //detentIndex:      0 1 2   3 4 5
            //actural position: 0 1 1+f 2 3 f
            //将手柄位置targetLeverIndex转换为襟翼指令targetDetentIndex
            if (leverIndex == 1 && adiru.adr.instrumentAirSpeed <= 100f) targetDetentIndex = 2;
            else if (leverIndex == 1 && adiru.adr.instrumentAirSpeed > 100f) targetDetentIndex = 1;
            else if (leverIndex == 0) targetDetentIndex = 0;
            else targetDetentIndex = leverIndex + 1;
            detentIndex = -targetDetentIndex-1;

            targetFlapAngle = flapDetents[targetDetentIndex];
            targetSlatAngle = slatDetents[targetDetentIndex];

            targetLift = liftMultiplier[targetDetentIndex];
            targetDrag = dragMultiplier[targetDetentIndex];

            targetSpeedLimit = speedLimits[targetDetentIndex];

            
            vehicleAnimator.SetFloat(flapLeverParameterName, (float)leverIndex / (flapLeverDetent - 1));
        }
        private void UpdateSounds(float deltaTime, bool actuatorAvailable) {
            var moving = actuatorAvailable && !Mathf.Approximately(targetFlapAngle, flapAngle) && !Mathf.Approximately(targetSlatAngle, slatAngle);

            for (var i = 0; i < audioSources.Length; i++) {
                var audioSource = audioSources[i];
                if (!audioSource) continue;

                var volume = Mathf.Lerp(audioSource.volume, moving ? audioVolumes[i] : 0.0f, soundResponse * deltaTime);
                var stop = Mathf.Approximately(volume, 0);

                if (stop) {
                    if (audioSource.isPlaying) {
                        audioSource.Stop();
                        audioSource.volume = 0;
                        audioSource.pitch = 0.8f;
                    }
                }
                else {
                    audioSource.volume = volume;
                    audioSource.pitch = Mathf.Lerp(audioSource.volume, (moving ? 1.0f : 0.8f) * audioPitches[i], soundResponse * deltaTime);

                    if (!audioSource.isPlaying) {
                        audioSource.loop = true;
                        audioSource.time = audioSource.clip.length * (Random.value % 1.0f);
                        audioSource.Play();
                    }
                }
            }
        }

        private void ApplyDamage(float deltaTime) {
            var airSpeed = airVehicle.AirSpeed * 1.94384f; // KAIS
            var damage = Mathf.Max(airSpeed - speedLimit, 0) / speedLimit * overspeedDamageMultiplier;
            if (damage > 0) {
                if (!actuatorBroken && Random.value < damage * deltaTime / meanTimeBetweenActuatorBrokenOnOverspeed) {
                    actuatorBroken = true;
                }

                if (!WingBroken && Random.value < damage * deltaTime / meanTimeBetweenWingBrokenOnOverspeed) {
                    WingBroken = true;
                    actuatorBroken = true;
                    ApplyParameters();
                }
            }
        }

        private float appliedExtraDrag, appliedExtraLift;
        private void ApplyParameters() {
            var extraDrag = WingBroken ? brokenDragMultiplier - 1 : (drag - 1);
            var extraLift = WingBroken ? brokenLiftMultiplier - 1 : (lift - 1);

            airVehicle.ExtraDrag += extraDrag - appliedExtraDrag;
            airVehicle.ExtraLift += extraLift - appliedExtraLift;

            appliedExtraDrag = extraDrag;
            appliedExtraLift = extraLift;
        }

        private void PlayHapticEvent() {
            var hand = trackingTarget == VRCPlayerApi.TrackingDataType.LeftHand ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right;
            Networking.LocalPlayer.PlayHapticEventInHand(hand, hapticDuration, hapticAmplitude, hapticFrequency);
        }
    }
}
