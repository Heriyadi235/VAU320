namespace A320VAU.FWS {
    public partial class FWSWarningData {


        private void SetupSpeed() {
            OVERSPEED = GetWarningMessageData(nameof(OVERSPEED));
        }

        private void MonitorSpeed() {
            var VFE = (int)FWS.equipmentData.flapTargetSpeedLimit;

            if (VFE < (int)FWS.equipmentData.flapCurrentSpeedLimit)
                VFE = (int)FWS.equipmentData.flapCurrentSpeedLimit;

            var VMAX = VMO;
            if (VFE < VMAX)
                VMAX = (int)FWS.equipmentData.flapTargetSpeedLimit;

            if (FWS.equipmentData.isGearsTargetDown && VLE < VMAX)
                VMAX = VLE;

            SetWarnVisible(ref OVERSPEED.isVisible, FWS.adiru.adr.instrumentAirSpeed > VMAX + 4f, true);
            if (OVERSPEED.isVisible) {
                SetWarnVisible(ref OVERSPEED.MessageLine[0].isMessageVisible, FWS.adiru.adr.instrumentAirSpeed > VMO);
                SetWarnVisible(ref OVERSPEED.MessageLine[1].isMessageVisible, FWS.adiru.adr.instrumentAirSpeed > VLE);
                SetWarnVisible(ref OVERSPEED.MessageLine[2].isMessageVisible, FWS.adiru.adr.instrumentAirSpeed > VFE);

                OVERSPEED.MessageLine[2].MessageText = $" -VFE................{VFE}";
            }
        }
    }
}