using UdonSharp;

namespace A320VAU.FWS {
    public partial class FWSWarningData : UdonSharpBehaviour {

        private void SetupGear() {
            BRAKES_HOT = GetWarningMessageData(nameof(BRAKES_HOT));
        }

        private void MonitorGear() {
            // We don't have brake hot simulate, so......
            // if (BRAKES_HOT.IsVisable)
            // {
            //     if (FWS.saccAirVehicle.Taxiing)
            //     {
            //         // Ground
            //         BRAKES_HOT.MessageLine[0].IsMessageVisible = true;
            //         BRAKES_HOT.MessageLine[1].IsMessageVisible = true;
            //         BRAKES_HOT.MessageLine[2].IsMessageVisible = true;
            //         // Air
            //         BRAKES_HOT.MessageLine[3].IsMessageVisible = false;
            //         BRAKES_HOT.MessageLine[4].IsMessageVisible = false;
            //         BRAKES_HOT.MessageLine[5].IsMessageVisible = false;
            //     }
            //     else
            //     {
            //         // Ground
            //         BRAKES_HOT.MessageLine[0].IsMessageVisible = false;
            //         BRAKES_HOT.MessageLine[1].IsMessageVisible = false;
            //         BRAKES_HOT.MessageLine[2].IsMessageVisible = false;
            //         // Air
            //         BRAKES_HOT.MessageLine[3].IsMessageVisible = true;
            //         BRAKES_HOT.MessageLine[4].IsMessageVisible = FWS.equipmentData.GearNose.targetPosition != 1;
            //         BRAKES_HOT.MessageLine[5].IsMessageVisible = true;
            //     }
            // }
        }
    }
}