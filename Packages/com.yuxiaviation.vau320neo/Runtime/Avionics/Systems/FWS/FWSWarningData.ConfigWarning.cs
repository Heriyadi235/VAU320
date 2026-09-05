using UnityEngine;

namespace A320VAU.FWS {
    public partial class FWSWarningData {

        private void SetupConfig() {
            FLAPS_NOT_IN_TAKEOFF_CONFIG = GetWarningMessageData(nameof(FLAPS_NOT_IN_TAKEOFF_CONFIG));
            PARK_BRAKE_ON = GetWarningMessageData(nameof(PARK_BRAKE_ON));
        }

        private void MonitorConfig() {
            if ((Mathf.Approximately(FWS.equipmentData.engine1ThrottleLeveler, 1f) ||
                 Mathf.Approximately(FWS.equipmentData.engine2ThrottleLeveler, 1f)) &&
                FWS.equipmentData.isAircraftGrounded) {
                SetWarnVisible(ref FLAPS_NOT_IN_TAKEOFF_CONFIG.isVisible,
                    !(FWS.equipmentData.flapCurrentIndex == 1 || FWS.equipmentData.flapCurrentIndex == 2), true);
                SetWarnVisible(ref PARK_BRAKE_ON.isVisible, FWS.equipmentData.isParkBreakSet, true);
            }
            else {
                SetWarnVisible(ref FLAPS_NOT_IN_TAKEOFF_CONFIG.isVisible, false, true);
                SetWarnVisible(ref PARK_BRAKE_ON.isVisible, false, true);
            }
        }
    }
}