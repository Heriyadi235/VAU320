using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

namespace VAU.V320NeoNext.Runtime.Systems.Seat
{
    public sealed class StationCameraShiftBugWorkaround : UdonSharpBehaviour
    {
        public Transform targetEyePosition;
        public Transform stationEnterPositionToAdjust;
        public float startDelay = 1;

        [Header("Debug Features (DO NO USE) (bad performance)")]
        public bool debugMode;

        public TextMeshProUGUI debugText;
        public Transform stationEnterPositionToAdjustForDebug;

        public KeyCode toggleActive = KeyCode.Keypad5;
        public KeyCode requestUpdateOneFrame = KeyCode.Keypad9;


        private bool _localPlayerInStationAndIsDesktop;
        private bool _activated;

        // Init in Start();
        private VRCCameraSettings _screenCameraSettings;
        private Vector3 _enterPositionInitialLocalPosition;
        private Transform _enterPositionParentTransform;

        private void Start()
        {
            _enterPositionInitialLocalPosition = stationEnterPositionToAdjust.localPosition;
            _enterPositionParentTransform = stationEnterPositionToAdjust.parent;
            _screenCameraSettings = VRCCameraSettings.ScreenCamera;
        }

        private float _lastUpdateTime;
        private bool _activeOnlyOneFrame;

        private float _lastEnterPositionLocalX;

        public override void PostLateUpdate()
        {
            if (debugMode)
            {
                if (Input.GetKeyDown(toggleActive))
                {
                    _activated = !_activated;
                }

                if (Input.GetKeyDown(requestUpdateOneFrame))
                {
                    _activeOnlyOneFrame = true;
                }
            }

            if (!_activated) return;

            var worldOffset = targetEyePosition.position - _screenCameraSettings.Position;
            var offset = _enterPositionParentTransform.InverseTransformVector(worldOffset);

            var enterPosition = stationEnterPositionToAdjust.localPosition;
            enterPosition.x = _lastEnterPositionLocalX;
            var target = enterPosition + offset;

            if (debugMode)
            {
                var cameraPos = _screenCameraSettings.Position;
                Debug.Log($"[{nameof(StationCameraShiftBugWorkaround)}]" +
                          $"Time:{Time.time}|DeltaTime:{Time.deltaTime}|" +
                          $"SeatWorldRotation:{stationEnterPositionToAdjust.rotation}|SeatWorldPosition:{stationEnterPositionToAdjust.position}|" +
                          $"CameraWorldPos:{cameraPos}|" +
                          $"TargetEyeWorldPos:{targetEyePosition.position}|TargetEyeLocalPos:{targetEyePosition.localPosition}|" +
                          $"SeatLocalPosition:{stationEnterPositionToAdjust.localPosition}|" +
                          $"WorldOffsetToMove:{worldOffset}|LocalOffsetToMove:{offset}|" +
                          $"NewSeatLocalPos: {target}|" +
                          $"Offset To InitialLocalPos:{target - _enterPositionInitialLocalPosition}|" +
                          $"SeatLocalPos:{stationEnterPositionToAdjust.localPosition}|TargetSeatLocalPos:{target}")
                    ;

                if (debugText)
                    debugText.text = $"WorldOffsetToMove: {worldOffset} LocalOffsetToMove: {offset}\n" +
                                     $"Leaped NewSeatLocalPos: {target}\n" +
                                     $"Offset To InitialLocalPos: {target - _enterPositionInitialLocalPosition}\n" +
                                     $"SeatLocalPos: {stationEnterPositionToAdjust.localPosition} TargetSeatLocalPos: {target}\n" +
                                     $"CameraWorldPos: {cameraPos}\n" +
                                     $"TargetEyeWorldPos: {targetEyePosition.position} TargetEyeLocalPos: {targetEyePosition.localPosition}";

                if (stationEnterPositionToAdjustForDebug) stationEnterPositionToAdjustForDebug.localPosition = target;
            }

            _lastEnterPositionLocalX = target.x;
            stationEnterPositionToAdjust.localPosition = target;

            if (_activeOnlyOneFrame) _activated = false;
        }

        public override void OnStationEntered(VRCPlayerApi player)
        {
            if (player.isLocal && !player.IsUserInVR())
            {
                _localPlayerInStationAndIsDesktop = true;
                SendCustomEventDelayedSeconds(nameof(_Active), startDelay);
                return;
            }

            _localPlayerInStationAndIsDesktop = false;
        }

        public override void OnStationExited(VRCPlayerApi player)
        {
            _localPlayerInStationAndIsDesktop = false;
            _activated = false;

            stationEnterPositionToAdjust.localPosition = _enterPositionInitialLocalPosition;
        }

        public void _Active()
        {
            if (!_localPlayerInStationAndIsDesktop)
            {
                _activated = false;
                return;
            }

            _activated = true;
        }
    }
}