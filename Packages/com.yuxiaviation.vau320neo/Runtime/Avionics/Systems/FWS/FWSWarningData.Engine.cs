namespace A320VAU.FWS {
    public partial class FWSWarningData {

        private void SetupEngine() {
            ENGINE1_FIRE = GetWarningMessageData(nameof(ENGINE1_FIRE));
            ENGINE2_FIRE = GetWarningMessageData(nameof(ENGINE2_FIRE));

            DUAL_ENGINE_FAULT = GetWarningMessageData(nameof(DUAL_ENGINE_FAULT));

            ENGINE1_SHUT_DOWN = GetWarningMessageData(nameof(ENGINE1_SHUT_DOWN));

            ENGINE1_FAIL = GetWarningMessageData(nameof(ENGINE1_FAIL));
            ENGINE2_FAIL = GetWarningMessageData(nameof(ENGINE2_FAIL));

            ENGINE1_EGT_OVERLIMIT = GetWarningMessageData(nameof(ENGINE1_EGT_OVERLIMIT));
            ENGINE2_EGT_OVERLIMIT = GetWarningMessageData(nameof(ENGINE2_EGT_OVERLIMIT));

            ENGINE1_N1_OVERLIMIT = GetWarningMessageData(nameof(ENGINE1_N1_OVERLIMIT));
            ENGINE2_N1_OVERLIMIT = GetWarningMessageData(nameof(ENGINE2_N1_OVERLIMIT));

            ENGINE1_N2_OVERLIMIT = GetWarningMessageData(nameof(ENGINE1_N2_OVERLIMIT));
            ENGINE2_N2_OVERLIMIT = GetWarningMessageData(nameof(ENGINE2_N2_OVERLIMIT));
        }

        private void MonitorEngine() {
            var engine1N1 = FWS.equipmentData.engine1n1 * 100;
            var engine2N1 = FWS.equipmentData.engine1n2 * 100;

            var engine1N2 = FWS.equipmentData.engine2n1 * 100;
            var engine2N2 = FWS.equipmentData.engine2n2 * 100;

        #region ENGIEN FIRE

            SetWarnVisible(ref ENGINE1_FIRE.isVisible, FWS.equipmentData.isEngine1Fire, true);
            if (ENGINE1_FIRE.isVisible) {
                SetWarnVisible(ref ENGINE1_FIRE.MessageLine[0].isMessageVisible,
                    !FWS.equipmentData.isEngine1ThrottleLevelerIdle);
                SetWarnVisible(ref ENGINE1_FIRE.MessageLine[1].isMessageVisible, FWS.equipmentData.isEngine1Running);
                SetWarnVisible(ref ENGINE1_FIRE.MessageLine[2].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FIRE.MessageLine[3].isMessageVisible, true);
            }

            SetWarnVisible(ref ENGINE2_FIRE.isVisible, FWS.equipmentData.isEngine2Fire, true);
            if (ENGINE2_FIRE.isVisible) {
                SetWarnVisible(ref ENGINE2_FIRE.MessageLine[0].isMessageVisible,
                    !FWS.equipmentData.isEngine2ThrottleLevelerIdle);
                SetWarnVisible(ref ENGINE2_FIRE.MessageLine[1].isMessageVisible, FWS.equipmentData.isEngine2Fire);
                SetWarnVisible(ref ENGINE2_FIRE.MessageLine[2].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FIRE.MessageLine[3].isMessageVisible, true);
            }

        #endregion

        #region DUAL ENGINE FAIL

            SetWarnVisible(ref DUAL_ENGINE_FAULT.isVisible,
                !FWS.equipmentData.isAircraftGrounded && !FWS.equipmentData.isEngine1Running &&
                !FWS.equipmentData.isEngine2Running, true);
            if (DUAL_ENGINE_FAULT.isVisible) {
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[0].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[1].isMessageVisible,
                    !FWS.equipmentData.isBothThrottleLevelerIdle);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[2].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[3].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[4].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[5].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[6].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[7].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[8].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[9].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[10].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[11].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[12].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[13].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[14].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[15].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[16].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[17].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[18].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[19].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[20].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[21].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[22].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[23].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[24].isMessageVisible, true);
            }

        #endregion

        #region ENGINE FAIL

            SetWarnVisible(ref ENGINE1_FAIL.isVisible,
                !FWS.equipmentData.isAircraftGrounded && !FWS.equipmentData.isEngine1Running, true);
            if (ENGINE1_FAIL.isVisible) {
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[0].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[1].isMessageVisible,
                    !FWS.equipmentData.isEngine1ThrottleLevelerIdle);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[2].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[3].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[4].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[5].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[6].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[7].isMessageVisible, true);
            }

            SetWarnVisible(ref ENGINE2_FAIL.isVisible,
                !FWS.equipmentData.isAircraftGrounded && !FWS.equipmentData.isEngine2Running, true);
            if (ENGINE2_FAIL.isVisible) {
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[0].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[1].isMessageVisible,
                    !FWS.equipmentData.isEngine2ThrottleLevelerIdle);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[2].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[3].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[4].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[5].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[6].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[7].isMessageVisible, true);
            }

        #endregion

        #region EGT Overlimit

            SetWarnVisible(ref ENGINE1_EGT_OVERLIMIT.isVisible, FWS.equipmentData.engine1EGT > 1060f, true);
            if (ENGINE1_EGT_OVERLIMIT.isVisible) {
                SetWarnVisible(ref ENGINE1_EGT_OVERLIMIT.MessageLine[0].isMessageVisible,
                    !FWS.equipmentData.isEngine1ThrottleLevelerIdle);
                SetWarnVisible(ref ENGINE1_EGT_OVERLIMIT.MessageLine[1].isMessageVisible,
                    FWS.equipmentData.isEngine1Fuel);
            }

            SetWarnVisible(ref ENGINE2_EGT_OVERLIMIT.isVisible, FWS.equipmentData.engine2EGT > 1060f, true);
            if (ENGINE2_EGT_OVERLIMIT.isVisible) {
                SetWarnVisible(ref ENGINE2_EGT_OVERLIMIT.MessageLine[0].isMessageVisible,
                    !FWS.equipmentData.isEngine2ThrottleLevelerIdle);
                SetWarnVisible(ref ENGINE2_EGT_OVERLIMIT.MessageLine[1].isMessageVisible,
                    FWS.equipmentData.isEngine2Fuel);
            }

        #endregion

        #region N1 OVERLIMIT

            SetWarnVisible(ref ENGINE1_N1_OVERLIMIT.isVisible, engine1N1 > 100f, true);
            if (ENGINE1_N1_OVERLIMIT.isVisible) {
                SetWarnVisible(ref ENGINE1_N1_OVERLIMIT.MessageLine[0].isMessageVisible,
                    !FWS.equipmentData.isEngine1ThrottleLevelerIdle);
                SetWarnVisible(ref ENGINE1_N1_OVERLIMIT.MessageLine[1].isMessageVisible,
                    FWS.equipmentData.isEngine1Fuel);
            }

            SetWarnVisible(ref ENGINE2_N1_OVERLIMIT.isVisible, engine2N1 > 100f, true);
            if (ENGINE2_N1_OVERLIMIT.isVisible) {
                SetWarnVisible(ref ENGINE2_N1_OVERLIMIT.MessageLine[0].isMessageVisible,
                    !FWS.equipmentData.isEngine2ThrottleLevelerIdle);
                SetWarnVisible(ref ENGINE2_N1_OVERLIMIT.MessageLine[1].isMessageVisible,
                    FWS.equipmentData.isEngine2Fuel);
            }

        #endregion

        #region N2 OVERLIMIT

            SetWarnVisible(ref ENGINE1_N2_OVERLIMIT.isVisible, engine1N2 > 100f, true);
            if (ENGINE1_N2_OVERLIMIT.isVisible) {
                SetWarnVisible(ref ENGINE1_N2_OVERLIMIT.MessageLine[0].isMessageVisible,
                    !FWS.equipmentData.isEngine1ThrottleLevelerIdle);
                SetWarnVisible(ref ENGINE1_N2_OVERLIMIT.MessageLine[1].isMessageVisible,
                    FWS.equipmentData.isEngine1Fuel);
            }

            SetWarnVisible(ref ENGINE2_N2_OVERLIMIT.isVisible, engine2N2 > 100f, true);
            if (ENGINE2_N2_OVERLIMIT.isVisible) {
                SetWarnVisible(ref ENGINE2_N2_OVERLIMIT.MessageLine[0].isMessageVisible,
                    !FWS.equipmentData.isEngine2ThrottleLevelerIdle);
                SetWarnVisible(ref ENGINE2_N2_OVERLIMIT.MessageLine[1].isMessageVisible,
                    FWS.equipmentData.isEngine2Fuel);
            }

        #endregion

            // setWarningMessageVisableValue(ref ENGINE1_SHUT_DOWN.IsVisable, !FWS.Engine1.starter);
            // if (ENGINE1_SHUT_DOWN.IsVisable)
            // {
            //     if (!FWS.Engine1.starter)
            //     {
            //         setWarningMessageVisableValue(ref ENGINE1_SHUT_DOWN.MessageLine[0].IsMessageVisible, false);
            //         setWarningMessageVisableValue(ref ENGINE1_SHUT_DOWN.MessageLine[1].IsMessageVisible, false);
            //         setWarningMessageVisableValue(ref ENGINE1_SHUT_DOWN.MessageLine[2].IsMessageVisible, false);
            //         setWarningMessageVisableValue(ref ENGINE1_SHUT_DOWN.MessageLine[3].IsMessageVisible, true);
            //         setWarningMessageVisableValue(ref ENGINE1_SHUT_DOWN.MessageLine[4].IsMessageVisible, true);
            //     }
            // }
        }
    }
}