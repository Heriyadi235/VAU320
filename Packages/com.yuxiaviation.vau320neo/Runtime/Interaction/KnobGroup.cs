using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace A320VAU.Interaction {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class KnobGroup : UdonSharpBehaviour {
        [Header("--- FCU 旋钮列表 ---")]
        [Tooltip("将 FCU 上的所有 GenericKnob 按从左到右顺序拖入此数组")]
        public GenericKnob[] knobs;

        [Header("--- Keybind Settings ---")]
        public KeyCode toggleMenuKey = KeyCode.Backslash; // `\` 键
        public KeyCode prevKnobKey = KeyCode.LeftBracket; // `[` 键
        public KeyCode nextKnobKey = KeyCode.RightBracket;// `]` 键

        private int activeKnobIndex = 0;
        private GenericKnob currentlyOpenKnob = null;

        private void Start() {
            if (knobs != null) {
                for (int i = 0; i < knobs.Length; i++) {
                    if (knobs[i] != null) {
                        knobs[i].knobGroup = this;
                    }
                }
            }
        }

        private void Update() {
            if (knobs == null || knobs.Length == 0) return;

            // 1. 按键 `\` 开关当前选中的旋钮菜单
            if (Input.GetKeyDown(toggleMenuKey)) {
                GenericKnob targetKnob = knobs[activeKnobIndex];
                if (targetKnob != null) {
                    if (currentlyOpenKnob == targetKnob) {
                        targetKnob.CloseMenu();
                    }
                    else {
                        RequestOpenMenu(targetKnob);
                    }
                }
            }

            // 2. 按键 `[` 切换上一个旋钮
            if (Input.GetKeyDown(prevKnobKey)) {
                SwitchActiveKnob(-1);
            }

            // 3. 按键 `]` 切换下一个旋钮
            if (Input.GetKeyDown(nextKnobKey)) {
                SwitchActiveKnob(1);
            }
        }

        private void SwitchActiveKnob(int direction) {
            activeKnobIndex = (activeKnobIndex + direction + knobs.Length) % knobs.Length;
            GenericKnob newKnob = knobs[activeKnobIndex];
            if (newKnob != null) {
                RequestOpenMenu(newKnob);
            }
        }

        /// <summary>
        /// 请求打开指定旋钮的菜单（排他性控制，自动关闭上一个）
        /// </summary>
        public void RequestOpenMenu(GenericKnob knob) {
            if (knob == null) return;

            // 如果当前已有其他旋钮打开，强制将其关闭
            if (currentlyOpenKnob != null && currentlyOpenKnob != knob) {
                currentlyOpenKnob.CloseMenu();
            }

            currentlyOpenKnob = knob;

            // 同步当前选中的旋钮索引
            for (int i = 0; i < knobs.Length; i++) {
                if (knobs[i] == knob) {
                    activeKnobIndex = i;
                    break;
                }
            }

            knob.OpenMenu();
        }

        /// <summary>
        /// 旋钮关闭时清理引用
        /// </summary>
        public void OnMenuClosed(GenericKnob knob) {
            if (currentlyOpenKnob == knob) {
                currentlyOpenKnob = null;
            }
        }

        public GenericKnob GetCurrentlyOpenKnob() {
            return currentlyOpenKnob;
        }
    }
}