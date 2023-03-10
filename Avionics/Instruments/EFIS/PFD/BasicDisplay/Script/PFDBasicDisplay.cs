
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using YuxiFlightInstruments.BasicFlightData;
using A320VAU.Avionics;

namespace A320VAU.PFD
{
    public class PFDBasicDisplay : UdonSharpBehaviour
    {
        [Tooltip("Flight Data Interface")]
        public YFI_FlightDataInterface FlightData;
        [Tooltip("RadioHeight")]
        public GPWS_OWML GPWSController;
        [Tooltip("FCU")]
        public FCU.FCU FCU;

        [Tooltip("仪表的动画控制器")]
        public Animator IndicatorAnimator;
        [Header("下面的量程都是单侧的")]
        [Tooltip("速度表最大量程(节)")]
        public float MAXSPEED = 600f;
        [Tooltip("俯仰角最大量程(度)")]
        public float MAXPITCH = 90f;
        [Tooltip("滚转角最大量程(度)")]
        public float MAXBANK = 180f;
        [Tooltip("高度表最大量程(英尺)")]
        public float MAXALT = 99990f;
        [Tooltip("标高指示范围")]
        public float MAXRHE = 600f;
        [Header("对于数字每一位都需要单独动画的仪表")]
        public bool altbybit = true;

        [Tooltip("一些罗盘动画起始角度并不为0")]
        public float HDGoffset = 180;
        [Tooltip("爬升率最大量程(英尺/分钟)")]
        public float MAXVS = 6000;
        //侧滑这个数值先固定着
        [Tooltip("最大侧滑角")]
        public float MAXSLIPANGLE = 40;

        [Tooltip("最大垂直侧滑")]
        public float MAXTRACKPITCH = 25;

        [Header("UI element")]
        public GameObject VSbackground;
        public Text VSText;
        public Text RadioHeightText;
        public Text MachNumberText;

        [Header("Speed element")]
        public GameObject[] disableOnGround;
        public GameObject[] enableOnGround;


        private float altitude = 0f;
        //animator strings that are sent every frame are converted to int for optimization
        private int AIRSPEED_HASH = Animator.StringToHash("AirSpeedNormalize");
        private int AIRSPEED_SECLECT_HASH = Animator.StringToHash("AirSpeedSelectNormalize");
        private int PITCH_HASH = Animator.StringToHash("PitchAngleNormalize");
        private int BANK_HASH = Animator.StringToHash("BankAngleNormalize");
        private int ALT_HASH = Animator.StringToHash("AltitudeNormalize");
        private int ALT10_HASH = Animator.StringToHash("Altitude10Normalize");
        private int ALT100_HASH = Animator.StringToHash("Altitude100Normalize");
        private int ALT1000_HASH = Animator.StringToHash("Altitude1000Normalize");
        private int ALT10000_HASH = Animator.StringToHash("Altitude10000Normalize");
        private int ROC_HASH = Animator.StringToHash("VerticalSpeedNormalize");
        private int HEADING_HASH = Animator.StringToHash("HeadingNormalize");
        private int SLIPANGLE_HASH = Animator.StringToHash("SlipAngleNormalize");
        private int RH_HASH = Animator.StringToHash("RHNormalize");
        private int TRKPCH_HASH = Animator.StringToHash("TRKPCHNormalize");
        //set default ball rotation here
        private Vector3 GyroBallRotationDefault;
        private float[] GyroBallFacotr = { -2f, 0f };

        private float PitchAngle = 0f;
        private float BankAngle = 0f;
        private float HeadingAngle = 0f;
        private float RadioHeight = 0f;
        void Start()
        {

        }

        private void LateUpdate()
        {
            //这里可以用来做仪表更新延迟之类的逻辑
            PitchAngle = FlightData.pitch;
            BankAngle = FlightData.bank;
            HeadingAngle = FlightData.magneticHeading;
            RadioHeight = (float)GPWSController.GetProgramVariable("radioAltitude");
            //AirSpeed
            UpdateAirspeed();
            //Altitude
            UpdateAltitude();
            //RH
            UpdateRadioHeight();
            //VS
            UpdateVerticalSpeed();
            //Heading
            UpdateHeading();
            //Bank
            UpdateBank();
            //Pitch
            UpdatePitch();
            //Slip
            UpdateSlip();
            //TrackPitch
            UpdateTrickPitch();

            UpdateMachNumber();

        }
        private void UpdateAirspeed()
        {
                foreach (var item in disableOnGround)
                {
                    item.SetActive(!FlightData.SAVControl.Taxiing);
                }
                foreach (var item in enableOnGround)
                {
                    item.SetActive(FlightData.SAVControl.Taxiing);
                }
            IndicatorAnimator.SetFloat(AIRSPEED_HASH, FlightData.TAS / MAXSPEED);
            // IndicatorAnimator.SetFloat(AIRSPEED_SECLECT_HASH, FCU.TargetSpeed / MAXSPEED);
        }

        private void UpdateAltitude()
        {

            //默认都会写Altitude
            altitude = FlightData.altitude;
            IndicatorAnimator.SetFloat(ALT_HASH, (altitude / MAXALT));
            if (altbybit)
            {
                IndicatorAnimator.SetFloat(ALT10_HASH, (altitude % 100) / 100f);
                IndicatorAnimator.SetFloat(ALT100_HASH, ((int)(altitude / 100f) % 10) / 10f);
                IndicatorAnimator.SetFloat(ALT1000_HASH, ((int)(altitude / 1000f) % 10) / 10f);
                IndicatorAnimator.SetFloat(ALT10000_HASH, ((int)(altitude / 10000f) % 10) / 10f);
            }

        }

        private void UpdateRadioHeight()
        {
            if (RadioHeight > 2500f)
            {
                RadioHeightText.gameObject.SetActive(false);
            }
            else
            {
                RadioHeightText.gameObject.SetActive(true);
                RadioHeightText.text = RadioHeight.ToString("f0");
                var RadioAltitudeNormal = Remap01(RadioHeight, -MAXRHE, MAXRHE);
                IndicatorAnimator.SetFloat(RH_HASH, RadioAltitudeNormal);
            }

        }
        private void UpdateVerticalSpeed()
        {
            var verticalSpeed = FlightData.verticalSpeed;
            float VerticalSpeedNormal = Remap01(FlightData.verticalSpeed, -MAXVS, MAXVS);
            IndicatorAnimator.SetFloat(ROC_HASH, VerticalSpeedNormal);
            if (Mathf.Abs(verticalSpeed) > 200)
            {
                VSbackground.SetActive(true);
                if (Mathf.Abs(verticalSpeed) > 6000) VSText.color = new Color(0.91373f, 0.54901f, 0);
                else if (1000 < RadioHeight && RadioHeight < 2000 && Mathf.Abs(verticalSpeed) > 2000) VSText.color = new Color(0.91373f, 0.54901f, 0);
                else if ((RadioHeight < 1200) && Mathf.Abs(verticalSpeed) > 1200) VSText.color = new Color(0.91373f, 0.54901f, 0);
                else VSText.color = new Color(0, 1, 0);
                VSText.text = (verticalSpeed / 100).ToString("f0");
            }
            else
            {
                VSbackground.gameObject.SetActive(false);
            }
        }
        private void UpdateHeading()
        {
            IndicatorAnimator.SetFloat(HEADING_HASH, ((HeadingAngle - HDGoffset + 360) % 360) / 360f);
        }
        private void UpdatePitch()
        {
            //玄学问题，Pitch 跟 Bank 调用不了Remap01??
            float PitchAngleNormal = Mathf.Clamp01((PitchAngle + MAXPITCH) / (MAXPITCH + MAXPITCH));
            IndicatorAnimator.SetFloat(PITCH_HASH, PitchAngleNormal);
        }
        private void UpdateBank()
        {
            float BankAngleNormal = Mathf.Clamp01((BankAngle + MAXBANK) / (MAXBANK + MAXBANK));
            IndicatorAnimator.SetFloat(BANK_HASH, BankAngleNormal);
        }
        private void UpdateSlip()
        {
            IndicatorAnimator.SetFloat(SLIPANGLE_HASH, Mathf.Clamp01((FlightData.SlipAngle + MAXSLIPANGLE) / (MAXSLIPANGLE + MAXSLIPANGLE)));
        }
        private void UpdateMachNumber()
        {
            if (FlightData.mach > 0.5f)
            {
                MachNumberText.gameObject.SetActive(true);
                MachNumberText.text = "." + (FlightData.mach * 100).ToString("f0");
            }
            else
            {
                MachNumberText.gameObject.SetActive(false);
            }
        }
        private void UpdateTrickPitch()
        {
            IndicatorAnimator.SetFloat(TRKPCH_HASH, Mathf.Clamp01((FlightData.trackPitchAngle + MAXTRACKPITCH) / (MAXTRACKPITCH + MAXTRACKPITCH)));
        }
        private float Remap01(float value, float valueMin, float valueMax)
        {
            value = Mathf.Clamp01((value - valueMin) / (valueMax - valueMin));
            return value;
        }
    }
}

