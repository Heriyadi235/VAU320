namespace A320VAU.FWS {
    public partial class FWSWarningData {

        private void SetupMemo() {
            APU_BLEED = GetWarningMessageData(nameof(APU_BLEED));
            PARK_BRK = GetWarningMessageData(nameof(PARK_BRK));
            APU_AVAIL = GetWarningMessageData(nameof(APU_AVAIL));
            SEAT_BELTS = GetWarningMessageData(nameof(SEAT_BELTS));
            NO_SMOKING = GetWarningMessageData(nameof(NO_SMOKING));
        }

        private void MonitorMemo() {
            SetWarnVisible(ref APU_BLEED.isVisible, FWS.equipmentData.isApuStarted);
            // APU BLEED will replace APU AVAIL if APU BLEED is on, but we don't have "APU BLEED" simulate.
            // SetWarnVisible(ref APU_AVAIL.IsVisable, FWS.equipmentData.IsAPURunning);
            SetWarnVisible(ref PARK_BRK.isVisible, FWS.equipmentData.isParkBreakSet);
            SetWarnVisible(ref SEAT_BELTS.isVisible, true);
            SetWarnVisible(ref NO_SMOKING.isVisible, true);
        }
    }
}