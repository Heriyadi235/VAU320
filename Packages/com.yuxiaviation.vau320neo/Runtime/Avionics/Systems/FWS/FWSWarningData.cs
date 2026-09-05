using UdonSharp;
using UnityEngine;

namespace A320VAU.FWS {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [DefaultExecutionOrder(2044)] //after ECAM
    public partial class FWSWarningData : UdonSharpBehaviour {
        private FWSWarningMessageData[] _fwsWarningMessageData;
        private bool _hasWarningDataVisibleChange;
        private bool _hasWarningVisibleChange;
        private FWS FWS;

        // IMPORTANT: DO NOT use fields in other partial class, it will mess up the version control system
        #region Partial Fields

        #region Config Memo

        private FWSWarningMessageData LANDING_MEMO;
        private FWSWarningMessageData TAKEOFF_MEMO;

        #endregion

        #region Config Warning

        private FWSWarningMessageData FLAPS_NOT_IN_TAKEOFF_CONFIG;
        private FWSWarningMessageData PARK_BRAKE_ON;

        #endregion

        #region Engine

        private FWSWarningMessageData DUAL_ENGINE_FAULT;

        private FWSWarningMessageData ENGINE1_EGT_OVERLIMIT;

        private FWSWarningMessageData ENGINE1_FAIL;
        private FWSWarningMessageData ENGINE1_FIRE;
        private FWSWarningMessageData ENGINE1_N1_OVERLIMIT;
        private FWSWarningMessageData ENGINE1_N2_OVERLIMIT;

        private FWSWarningMessageData ENGINE1_SHUT_DOWN;
        private FWSWarningMessageData ENGINE2_EGT_OVERLIMIT;
        private FWSWarningMessageData ENGINE2_FAIL;
        private FWSWarningMessageData ENGINE2_FIRE;
        private FWSWarningMessageData ENGINE2_N1_OVERLIMIT;
        private FWSWarningMessageData ENGINE2_N2_OVERLIMIT;

        #endregion

        #region Gear

        private FWSWarningMessageData BRAKES_HOT;

        #endregion

        #region Memo

        private FWSWarningMessageData APU_AVAIL;
        private FWSWarningMessageData APU_BLEED;
        private FWSWarningMessageData PARK_BRK;
        private FWSWarningMessageData SEAT_BELTS;
        private FWSWarningMessageData NO_SMOKING;

        #endregion

        #region Speed

        private FWSWarningMessageData OVERSPEED;
        private int VLE = 280;

        private int VMO = 350;

        #endregion

        #endregion
        private void Start() {
            _fwsWarningMessageData = GetComponentsInChildren<FWSWarningMessageData>();

            SetupEngine();
            SetupConfigMemo();
            SetupGear();
            SetupMemo();
            SetupConfig();
            SetupSpeed();
        }

        private FWSWarningMessageData GetWarningMessageData(string id) {
            foreach (var data in _fwsWarningMessageData) {
                if (data.Id != id) continue;
                return data;
            }

            return null;
        }

        public void Monitor(FWS fws) {
            _hasWarningVisibleChange = false;
            _hasWarningDataVisibleChange = false;
            FWS = fws;

            MonitorEngine();
            MonitorConfigMemo();
            MonitorGear();
            MonitorMemo();
            MonitorConfig();
            MonitorSpeed();

            fws._hasWarningDataVisibleChange = _hasWarningDataVisibleChange;
            fws._hasWarningVisibleChange = _hasWarningVisibleChange;
        }

        private void SetWarnVisible(ref bool isVisible, bool newValue, bool isWarnData = false) {
            if (isVisible == newValue) return;
            if (isWarnData) _hasWarningDataVisibleChange = true;

            isVisible = newValue;
            _hasWarningVisibleChange = true;
        }
    }
}