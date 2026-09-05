using System.Diagnostics.Eventing.Reader;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace A320VAU.Interaction {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class GenericKnob : UdonSharpBehaviour {
        [Header("--- Target System Configuration ---")]
        [Tooltip("接收事件的目标 Udon 脚本 (如 FCU 主控)")]
        public UdonSharpBehaviour targetBehaviour;

        [Tooltip("所属的 KnobGroup 管理器 (可在 KnobGroup 中自动绑定)")]
        public KnobGroup knobGroup;

        [Header("--- Knob Options Arrays (各数组长度保持一致) ---")]
        public string[] optionNames;
        [Tooltip("每个选项对应的高亮 TMP")]
        public TextMeshProUGUI[] highlightGraphics;
        [Tooltip("按下摇杆/鼠标中键点击时调用的事件名")]
        public string[] onClickEventNames;
        [Tooltip("手腕顺时针 / 滚轮向上时调用的事件名")]
        public string[] onClockwiseEventNames;
        [Tooltip("手腕逆时针 / 滚轮向下时调用的事件名")]
        public string[] onCounterClockwiseEventNames;

        [Header("--- Visual References ---")]
        public GameObject floatingMenuCanvas;
        public LineRenderer guideLine;
        public Transform knobTransform;

        public Color activeColor = new Color(0.9960785f, 0.6196079f, 0.2078432f, 0.8f);
        public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        [Header("--- Distance & Input Tuning ---")]
        //[Tooltip("手部/准心靠近旋钮唤醒菜单的距离(米)")]
        public float activationDistance = 0.03f;
        [Tooltip("VR 手腕旋转触发防抖角度(度)")]
        public float wristAngleThreshold = 4.0f;
        [Tooltip("PC 滚轮响应冷却时间(秒)")]
        public float pcScrollCooldown = 0.05f;

        // 内部状态
        private VRCPlayerApi localPlayer;
        private bool isVR = false;
        private bool isHandInside = false;

        // VR 状态
        private Quaternion lastHandRotation;
        private bool isTriggerPressed = false;
        private float LastStickPosY = 0;
        private bool isLeftHand = true;
        // Common / UI 状态
        private int currentOptionIndex = 0;
        private float lastScrollTime = 0f;
        private bool isMenuVisible = false;

        // 防抖坐标缓存 (Player Space Offset)
        private Vector3 menuOffsetFromKnob = new Vector3(0f, 0.16f, 0f);

        //更新频率控制
        private float updateTimer = 0f;
        public float updateInterval = 0.05f; // 20Hz 抽样率

        private void Start() {
            localPlayer = Networking.LocalPlayer;
            if (localPlayer != null && localPlayer.IsValid()) {
                isVR = localPlayer.IsUserInVR();
            }       
            if (isVR) {
                DisableInteractive = true;
            }
            else {
                this.enabled = false;
            }
            SetMenuVisible(false);
        }


        //VR状态-手指进入碰撞区域激活
        public override void PostLateUpdate() {
            updateTimer += Time.deltaTime;
            if (updateTimer > updateInterval) {
                //只有VR下需要激活判断手腕转角
                if (localPlayer == null || !localPlayer.IsValid()) return;
                // 1. 手部 / 视角靠近检测
                CheckProximity();
                updateTimer = 0;
            }
            // 2. 输入响应
            if (isVR && isHandInside) {
                HandleStickSelection();
                HandleVRWristRotation();
                
            }
            if (!isVR && isMenuVisible){
                if (Input.GetKeyDown(KeyCode.Mouse0)) {
                    CloseMenu();
                }
                HandleRMBSelection();
                HandlePCMouseScroll();
            }
        }

        // 关键防抖：在 LateUpdate 中跟随相机与载具物理更新后的最终位置计算 Canvas
        public void LateUpdate() {
            if (!isMenuVisible || floatingMenuCanvas == null || knobTransform == null) return;

            VRCPlayerApi.TrackingData headData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

            // 1. 将 Canvas 渲染位置置于旋钮上方，但旋转完全面向玩家头部 (Billboard 贴合玩家视野)
            Vector3 targetMenuPos = knobTransform.position + knobTransform.TransformDirection(menuOffsetFromKnob);
            floatingMenuCanvas.transform.position = targetMenuPos;

            // 使 UI 始终正对玩家 Head，消除运动机械倾斜
            Quaternion lookRot = Quaternion.LookRotation(targetMenuPos - headData.position, headData.rotation * Vector3.up);
            floatingMenuCanvas.transform.rotation = lookRot;

            // 2. 更新指引线
            if (guideLine != null && guideLine.enabled) {
                guideLine.SetPosition(0, knobTransform.position);
                guideLine.SetPosition(1, targetMenuPos);
            }
        }


        // 1. VR 靠近并按下扳机打开或关闭菜单
        private void CheckProximity() {
            if (knobTransform == null) return;

            Vector3 knobPos = knobTransform.position;
            bool isNear = false;

            if (isVR) {
                // VR 模式：检测左右手距离
                Vector3 rightHandPos = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
                Vector3 leftHandPos = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;

                float distRight = Vector3.Distance(knobPos, rightHandPos);
                float distLeft = Vector3.Distance(knobPos, leftHandPos);

                
                if((knobPos- rightHandPos).sqrMagnitude<= activationDistance * activationDistance ||
                    (knobPos - leftHandPos).sqrMagnitude <= activationDistance * activationDistance) {
                    isNear = true;
                    isLeftHand = (distRight <= distLeft) ? false : true;

                    //如果手第一次进入按钮
                    if (isNear && !isHandInside) {
                        if (isLeftHand) {
                            isTriggerPressed = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryIndexTrigger") > 0.8 ? true : false;
                        }
                        else {
                            isTriggerPressed = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger") > 0.8 ? true : false;
                        }

                        if (isTriggerPressed)//并且正按下扳机
                        {
                            //更新进入状态，激活菜单，并且记录手的初始旋转
                            isHandInside = true;
                            if (isLeftHand) lastHandRotation = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation;
                            else lastHandRotation = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation;
                            SetMenuVisible(true);
                            UpdateHighlight();
                        }
                        else {
                            SetMenuVisible(false);
                        }
                    }
                    //如果手已经在按钮中
                    if (isNear && isHandInside) {
                        if (isLeftHand) {
                            isTriggerPressed = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryIndexTrigger") > 0.8 ? true : false;
                        }
                        else {
                            isTriggerPressed = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger") > 0.8 ? true : false;
                        }
                        if (isTriggerPressed)//并且正在按下扳机
                        {
                            if (!isMenuVisible) {
                                if (knobGroup != null) {
                                    knobGroup.RequestOpenMenu(this);
                                }
                                else {
                                    OpenMenu();
                                }
                            }
                            else {
                                //HandleStickSelection();
                                //HandleVRWristRotation();
                            }
                        }
                        else {
                            // 如果自己当前是开启状态，再次点击左键则关闭
                            CloseMenu();
                        }
                    }
                    //如果手正在离开按钮
                    if (!isNear && isMenuVisible) {
                        isHandInside = false;
                        SetMenuVisible(false);
                    }   
                }
            }
        }


        // 1. PC 鼠标点击碰撞体打开或关闭菜单
        // PC 模式下的鼠标左键点击交互
        public override void Interact() {
            if (isVR) return;

            if (isMenuVisible) {
                // 如果自己当前是开启状态，再次点击左键则关闭
                CloseMenu();
            }
            else {
                // 如果自己未开启，向组管理器请求开启（组管理器会自动关掉其他已开启的旋钮）
                if (knobGroup != null) {
                    knobGroup.RequestOpenMenu(this);
                }
                else {
                    OpenMenu();
                }
            }
        }

        // 2. VR 下使用手柄摇杆切换菜单选项
        private void HandleStickSelection() {
            var StickPosY = isLeftHand ? Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryThumbstickVertical") :
                    Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryThumbstickVertical"); ;
            if (StickPosY > 0.75 && LastStickPosY <= 0.75) {
                currentOptionIndex = (currentOptionIndex - 1 + optionNames.Length) % optionNames.Length;
            }
            if (StickPosY < 0.25 && LastStickPosY >= 0.25) { 
                currentOptionIndex = (currentOptionIndex + 1) % optionNames.Length;
            }
            LastStickPosY = StickPosY;
            UpdateHighlight();
        }
        // 3. PC 下使用鼠标右键切换菜单选项
        private void HandleRMBSelection() {
            if (Input.GetKeyDown(KeyCode.Mouse1)) {
                currentOptionIndex = (currentOptionIndex + 1) % optionNames.Length;
                UpdateHighlight();
            }
        }

        // 2. PC 模式：鼠标滚轮调节
        private void HandlePCMouseScroll() {
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (Mathf.Abs(scroll) > 0.01f && Time.time - lastScrollTime > pcScrollCooldown) {
                lastScrollTime = Time.time;

                if (scroll > 0) {
                    SendEventToTarget(onClockwiseEventNames);
                    //if (knobTransform != null) knobTransform.Rotate(Vector3.forward, 15f, Space.Self);
                }
                else {
                    SendEventToTarget(onCounterClockwiseEventNames);
                    //if (knobTransform != null) knobTransform.Rotate(Vector3.forward, -15f, Space.Self);
                }
            }
        }

        // 3. VR 模式：手腕旋转调节
        private void HandleVRWristRotation() 
        {
            if (!isTriggerPressed) return;

            VRCPlayerApi.TrackingDataType handType = isLeftHand ?
                VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand;

            Quaternion currentRot = localPlayer.GetTrackingData(handType).rotation;

            // 1. 计算两帧间相对姿态变换 Delta
            Quaternion deltaRot = currentRot * Quaternion.Inverse(lastHandRotation);

            // 2. 转化为轴角表达
            deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;

            // 3. 提取沿手部指向方向 (Vector3.forward) 的纯扭转角度 Roll Delta
            Vector3 handForward = currentRot * Vector3.forward;
            float rollDelta = angle * Vector3.Dot(axis, handForward);

            // 4. 防抖门槛判定与事件回调
            if (Mathf.Abs(rollDelta) >= wristAngleThreshold) {
                if (rollDelta > 0) {
                    SendEventToTarget(onClockwiseEventNames);
                    //if (knobTransform != null) knobTransform.Rotate(Vector3.forward, 15f, Space.Self);
                } else {
                    SendEventToTarget(onCounterClockwiseEventNames);
                    //if (knobTransform != null) knobTransform.Rotate(Vector3.forward, -15f, Space.Self);
                }

            // 触发后刷新上次姿态，准备下一次累积
            lastHandRotation = currentRot;
            }
        }
        public void OpenMenu() {
            SetMenuVisible(true);
            this.enabled = true;
        }

        public void CloseMenu() {
            SetMenuVisible(false);
            this.enabled = false;
            if (knobGroup != null) {
                knobGroup.OnMenuClosed(this);
            }
        }

        // 5. 内部通用方法
        private void SendEventToTarget(string[] eventNameArray) {
            if (targetBehaviour == null || eventNameArray == null) return;
            if (currentOptionIndex < 0 || currentOptionIndex >= eventNameArray.Length) return;

            string eventName = eventNameArray[currentOptionIndex];
            if (!string.IsNullOrEmpty(eventName)) {
                targetBehaviour.SendCustomEvent(eventName);
            }
        }

        private void UpdateHighlight() {
            if (highlightGraphics == null) return;

            for (int i = 0; i < highlightGraphics.Length; i++) {
                if (highlightGraphics[i] != null) {
                    highlightGraphics[i].color = (i == currentOptionIndex) ? activeColor : normalColor;
                }
            }
        }

        private void SetMenuVisible(bool visible) {
            isMenuVisible = visible;
            currentOptionIndex = 0;
            UpdateHighlight();
            if (floatingMenuCanvas != null) floatingMenuCanvas.SetActive(visible);
            if (guideLine != null) guideLine.enabled = visible;
            if (!visible) currentOptionIndex = -1;


        }
    }
}
