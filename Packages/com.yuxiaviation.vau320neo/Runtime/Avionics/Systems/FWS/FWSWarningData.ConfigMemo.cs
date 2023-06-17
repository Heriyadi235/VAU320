﻿using UdonSharp;
using UnityEngine;

namespace A320VAU.FWS
{
    public partial class FWSWarningData : UdonSharpBehaviour
    {
        private FWSWarningMessageData TAKEOFF_MEMO;
        private FWSWarningMessageData LANDING_MEMO;

        private void SetupConfigMemo()
        {
            TAKEOFF_MEMO = GetWarningMessageData(nameof(TAKEOFF_MEMO));
            LANDING_MEMO = GetWarningMessageData(nameof(LANDING_MEMO));
        }

        private void MonitorConfigMemo()
        {
            var isEngine1Running = FWS.equipmentData.IsEngineLRunning;
            var isEngine2Running = FWS.equipmentData.IsEngineRRunning;

            #region Takeoff Memo

            SetWarnVisible(ref TAKEOFF_MEMO.isVisable,
                FWS.saccAirVehicle.Taxiing && !Mathf.Approximately(FWS.saccAirVehicle.ThrottleInput, 1) &&
                (isEngine1Running || isEngine2Running), true);
            if (TAKEOFF_MEMO.isVisable)
            {
                // AUTO BRK MAX
                TAKEOFF_MEMO.MessageLine[0].isMessageVisible = false;
                TAKEOFF_MEMO.MessageLine[1].isMessageVisible = false;
                
                TAKEOFF_MEMO.MessageLine[2].isMessageVisible = true;
                // SIGN ON
                TAKEOFF_MEMO.MessageLine[3].isMessageVisible = false;
                TAKEOFF_MEMO.MessageLine[4].isMessageVisible = false;
                
                TAKEOFF_MEMO.MessageLine[5].isMessageVisible = true;
                // CABIN READY
                if (!FWS.equipmentData.Canopy.CanopyOpen)
                {
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[6].isMessageVisible, false);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[7].isMessageVisible, false);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[8].isMessageVisible, true);
                }
                else
                {
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[6].isMessageVisible, true);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[7].isMessageVisible, true);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[8].isMessageVisible, false);
                }

                // SPLRS ARM
                SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[9].isMessageVisible, false);
                SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[10].isMessageVisible, false);
                
                SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[11].isMessageVisible, true);
                // FLAP T.O & T.O CONFIG TEST
                if ((FWS.equipmentData.Flap.detentIndex == 1 && FWS.equipmentData.Flap.targetDetentIndex == 1) ||
                    (FWS.equipmentData.Flap.detentIndex == 2 && FWS.equipmentData.Flap.targetDetentIndex == 2))
                {
                    // FLAP T.O
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[12].isMessageVisible, false);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[13].isMessageVisible, false);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[14].isMessageVisible, true);
                    // T.O CONFIG
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[15].isMessageVisible, false);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[16].isMessageVisible, false);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[17].isMessageVisible, true);
                }
                else
                {
                    // FLAP T.O
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[12].isMessageVisible, true);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[13].isMessageVisible, true);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[14].isMessageVisible, false);
                    // T.O CONFIG
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[15].isMessageVisible, true);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[16].isMessageVisible, true);
                    SetWarnVisible(ref TAKEOFF_MEMO.MessageLine[17].isMessageVisible, false);
                }
            }

            #endregion

            #region Landing Memo

            SetWarnVisible(ref LANDING_MEMO.isVisable,
                !FWS.saccAirVehicle.Taxiing & FWS.flightData.TAS > 80f & (isEngine1Running | isEngine2Running) &
                (float)FWS.gpws.GetProgramVariable("radioAltitude") < 2000f);

            if (LANDING_MEMO.isVisable)
            {
                // GEAR DN
                if (FWS.equipmentData.GearLeft.targetPosition == 1)
                {
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[0].isMessageVisible, false);
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[1].isMessageVisible, false);
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[2].isMessageVisible, true);
                }
                else
                {
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[0].isMessageVisible, true);
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[1].isMessageVisible, true);
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[2].isMessageVisible, false);
                }

                // SINGS ON
                LANDING_MEMO.MessageLine[3].isMessageVisible = false;
                LANDING_MEMO.MessageLine[4].isMessageVisible = false;
                LANDING_MEMO.MessageLine[5].isMessageVisible = true;

                // CABIN READY
                LANDING_MEMO.MessageLine[6].isMessageVisible = false;
                LANDING_MEMO.MessageLine[7].isMessageVisible = false;
                LANDING_MEMO.MessageLine[8].isMessageVisible = true;

                // SPLRS ARM
                LANDING_MEMO.MessageLine[9].isMessageVisible = false;
                LANDING_MEMO.MessageLine[10].isMessageVisible = false;
                LANDING_MEMO.MessageLine[11].isMessageVisible = true;

                // FLAPS FULL
                if (FWS.equipmentData.Flap.targetDetentIndex == 4)
                {
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[12].isMessageVisible, false);
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[13].isMessageVisible, false);
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[14].isMessageVisible, true);
                }
                else
                {
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[12].isMessageVisible, true);
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[13].isMessageVisible, true);
                    SetWarnVisible(ref LANDING_MEMO.MessageLine[14].isMessageVisible, false);
                }

                // FLAPS CONF3
                LANDING_MEMO.MessageLine[15].isMessageVisible = false;
                LANDING_MEMO.MessageLine[16].isMessageVisible = false;
            }

            #endregion
        }
    }
}