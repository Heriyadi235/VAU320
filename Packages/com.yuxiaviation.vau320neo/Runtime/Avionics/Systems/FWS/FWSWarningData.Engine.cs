﻿using UnityEngine;
using UdonSharp;

namespace A320VAU.FWS
{
    public partial class FWSWarningData : UdonSharpBehaviour
    {
        private FWSWarningMessageData ENGINE1_FIRE;
        private FWSWarningMessageData ENGINE2_FIRE;

        private FWSWarningMessageData DUAL_ENGINE_FAULT;

        private FWSWarningMessageData ENGINE1_SHUT_DOWN;

        private FWSWarningMessageData ENGINE1_FAIL;
        private FWSWarningMessageData ENGINE2_FAIL;

        private FWSWarningMessageData ENGINE1_EGT_OVERLIMIT;
        private FWSWarningMessageData ENGINE2_EGT_OVERLIMIT;
        private FWSWarningMessageData ENGINE1_N1_OVERLIMIT;
        private FWSWarningMessageData ENGINE2_N1_OVERLIMIT;
        private FWSWarningMessageData ENGINE1_N2_OVERLIMIT;
        private FWSWarningMessageData ENGINE2_N2_OVERLIMIT;

        private void SetupEngine()
        {
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

        private void MonitorEngine()
        {
            var engine1N1 = FWS.equipmentData.N1LRef * 100;
            var engine2N1 = FWS.equipmentData.N2LRef * 100;

            var engine1N2 = FWS.equipmentData.N2RRef * 100;
            var engine2N2 = FWS.equipmentData.N2RRef * 100;

            #region ENGIEN FIRE
            SetWarnVisible(ref ENGINE1_FIRE.isVisable, FWS.equipmentData.EngineL.fire, true);
            if (ENGINE1_FIRE.isVisable)
            {
                SetWarnVisible(ref ENGINE1_FIRE.MessageLine[0].isMessageVisible, !Mathf.Approximately(FWS.saccAirVehicle.ThrottleInput, FWS.equipmentData.EngineL.idlePoint));
                SetWarnVisible(ref ENGINE1_FIRE.MessageLine[1].isMessageVisible, FWS.equipmentData.EngineL.fuel);
                SetWarnVisible(ref ENGINE1_FIRE.MessageLine[2].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FIRE.MessageLine[3].isMessageVisible, true);
            }

            SetWarnVisible(ref ENGINE2_FIRE.isVisable, FWS.equipmentData.EngineR.fire, true);
            if (ENGINE2_FIRE.isVisable)
            {
                SetWarnVisible(ref ENGINE2_FIRE.MessageLine[0].isMessageVisible, !Mathf.Approximately(FWS.saccAirVehicle.ThrottleInput, FWS.equipmentData.EngineR.idlePoint));
                SetWarnVisible(ref ENGINE2_FIRE.MessageLine[1].isMessageVisible, FWS.equipmentData.EngineR.fuel);
                SetWarnVisible(ref ENGINE2_FIRE.MessageLine[2].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FIRE.MessageLine[3].isMessageVisible, true);
            }
            #endregion

            #region DUAL ENGINE FAIL
            SetWarnVisible(ref DUAL_ENGINE_FAULT.isVisable,
                !FWS.saccAirVehicle.Taxiing && !FWS.equipmentData.IsEngineLRunning &&
                !FWS.equipmentData.IsEngineRRunning, true);
            if (DUAL_ENGINE_FAULT.isVisable)
            {
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[0].isMessageVisible, true);
                SetWarnVisible(ref DUAL_ENGINE_FAULT.MessageLine[1].isMessageVisible, !Mathf.Approximately(FWS.equipmentData.ThrottleLevelerL, FWS.equipmentData.EngineL.idlePoint));
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
            SetWarnVisible(ref ENGINE1_FAIL.isVisable, !FWS.saccAirVehicle.Taxiing && !FWS.equipmentData.IsEngineLRunning, true);
            if (ENGINE1_FAIL.isVisable)
            {
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[0].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[1].isMessageVisible, !Mathf.Approximately(FWS.equipmentData.ThrottleLevelerL, FWS.equipmentData.EngineL.idlePoint));
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[2].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[3].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[4].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[5].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[6].isMessageVisible, true);
                SetWarnVisible(ref ENGINE1_FAIL.MessageLine[7].isMessageVisible, true);
            }

            SetWarnVisible(ref ENGINE2_FAIL.isVisable, !FWS.saccAirVehicle.Taxiing && !FWS.equipmentData.IsEngineRRunning, true);
            if (ENGINE2_FAIL.isVisable)
            {
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[0].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[1].isMessageVisible, !Mathf.Approximately(FWS.equipmentData.ThrottleLevelerR, FWS.equipmentData.EngineR.idlePoint));
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[2].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[3].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[4].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[5].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[6].isMessageVisible, true);
                SetWarnVisible(ref ENGINE2_FAIL.MessageLine[7].isMessageVisible, true);
            }
            #endregion

            #region EGT Overlimit
            SetWarnVisible(ref ENGINE1_EGT_OVERLIMIT.isVisable, FWS.equipmentData.EGTL > 1060f, true);
            if (ENGINE1_EGT_OVERLIMIT.isVisable)
            {
                SetWarnVisible(ref ENGINE1_EGT_OVERLIMIT.MessageLine[0].isMessageVisible, !Mathf.Approximately(FWS.equipmentData.ThrottleLevelerL, FWS.equipmentData.EngineL.idlePoint));
                SetWarnVisible(ref ENGINE1_EGT_OVERLIMIT.MessageLine[1].isMessageVisible, FWS.equipmentData.EngineL.fuel);
            }

            SetWarnVisible(ref ENGINE2_EGT_OVERLIMIT.isVisable, FWS.equipmentData.EGTR > 1060f, true);
            if (ENGINE2_EGT_OVERLIMIT.isVisable)
            {
                SetWarnVisible(ref ENGINE2_EGT_OVERLIMIT.MessageLine[0].isMessageVisible, !Mathf.Approximately(FWS.equipmentData.ThrottleLevelerR, FWS.equipmentData.EngineR.idlePoint));
                SetWarnVisible(ref ENGINE2_EGT_OVERLIMIT.MessageLine[1].isMessageVisible, FWS.equipmentData.EngineR.fuel);
            }
            #endregion

            #region N1 OVERLIMIT
            SetWarnVisible(ref ENGINE1_N1_OVERLIMIT.isVisable, engine1N1 > 100f, true);
            if (ENGINE1_N1_OVERLIMIT.isVisable)
            {
                SetWarnVisible(ref ENGINE1_N1_OVERLIMIT.MessageLine[0].isMessageVisible, !Mathf.Approximately(FWS.equipmentData.ThrottleLevelerL, FWS.equipmentData.EngineL.idlePoint));
                SetWarnVisible(ref ENGINE1_N1_OVERLIMIT.MessageLine[1].isMessageVisible, FWS.equipmentData.EngineL.fuel);
            }

            SetWarnVisible(ref ENGINE2_N1_OVERLIMIT.isVisable, engine2N1 > 100f, true);
            if (ENGINE2_N1_OVERLIMIT.isVisable)
            {
                SetWarnVisible(ref ENGINE2_N1_OVERLIMIT.MessageLine[0].isMessageVisible, !Mathf.Approximately(FWS.equipmentData.ThrottleLevelerR, FWS.equipmentData.EngineR.idlePoint));
                SetWarnVisible(ref ENGINE2_N1_OVERLIMIT.MessageLine[1].isMessageVisible, FWS.equipmentData.EngineR.fuel);
            }
            #endregion

            #region N2 OVERLIMIT
            SetWarnVisible(ref ENGINE1_N2_OVERLIMIT.isVisable, engine1N2 > 100f, true);
            if (ENGINE1_N2_OVERLIMIT.isVisable)
            {
                SetWarnVisible(ref ENGINE1_N2_OVERLIMIT.MessageLine[0].isMessageVisible, !Mathf.Approximately(FWS.equipmentData.ThrottleLevelerL, FWS.equipmentData.EngineL.idlePoint));
                SetWarnVisible(ref ENGINE1_N2_OVERLIMIT.MessageLine[1].isMessageVisible, FWS.equipmentData.EngineL.fuel);
            }

            SetWarnVisible(ref ENGINE2_N2_OVERLIMIT.isVisable, engine2N2 > 100f, true);
            if (ENGINE2_N2_OVERLIMIT.isVisable)
            {
                SetWarnVisible(ref ENGINE2_N2_OVERLIMIT.MessageLine[0].isMessageVisible, !Mathf.Approximately(FWS.equipmentData.ThrottleLevelerR, FWS.equipmentData.EngineR.idlePoint));
                SetWarnVisible(ref ENGINE2_N2_OVERLIMIT.MessageLine[1].isMessageVisible, FWS.equipmentData.EngineR.fuel);
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