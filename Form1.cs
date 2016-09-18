using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace RecController {
    public partial class Form1 : Form {
        bool isWorking = false;
        bool setKey = false;
        bool coolDown = false;
        string microName = "";
        int keyCode = -1;
        MMDevice micro;
        MMDevice[] allDevices;
        Hook _hook;

        public Form1() {
            InitializeComponent();
            KeyPreview = true;
            button1.Click += button1_Click;
            textBox2.Click += TextBox2_Click;
            comboBox1.TextChanged += ComboBox1_TextChanged;
            KeyDown += Form1_KeyDown;
            FormClosed += Form1_FormClosed;

            var devices = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            allDevices = new MMDevice[devices.Count];
            for (int i = 0; i < devices.Count; i++) {
                allDevices[i] = devices[i];
                comboBox1.Items.Add(devices[i].DeviceFriendlyName);
            }

            if (!File.Exists($@"{Environment.CurrentDirectory}\config.xml"))
                using (var stream = new FileStream($@"{Environment.CurrentDirectory}\config.xml", FileMode.OpenOrCreate)) { }
            var str = File.ReadAllLines($@"{Environment.CurrentDirectory}\config.xml");
            
            if (str.Length >= 3) {
                microName = str[0];

                int res;
                if (int.TryParse(str[1], out res))
                    keyCode = res;

                for (int i = 0; i < allDevices.Length; i++) {
                    if (allDevices[i].DeviceFriendlyName == microName)
                        comboBox1.SelectedItem = microName;
                }
                textBox2.Text = str[2];
            }
        }

        private void ComboBox1_TextChanged(object sender, EventArgs e) {
            microName = comboBox1.SelectedItem.ToString();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e) {
            if (setKey) {
                keyCode = (int)e.KeyCode;
                textBox2.Text = e.KeyCode.ToString();
                setKey = false;
            }
        }
        private void TextBox2_Click(object sender, EventArgs e) {
            if(!isWorking)
                setKey = true;
        }
        private void button1_Click(object sender, EventArgs e) {
            if (!isWorking) {
                for (int i = 0; i < allDevices.Length; i++) {
                    if (allDevices[i].DeviceFriendlyName == microName)
                        micro = allDevices[i];
                }

                if (micro != null) {
                    _hook = new Hook(keyCode);
                    _hook.KeyPressed += _hook_KeyPressed;
                    _hook.SetHook();
                    label1.Text = "            Микрофон";
                } else {
                    label1.Text = "МИКРОФОН НЕ НАЙДЕН";
                }
                button1.Text = micro.AudioEndpointVolume.Mute ? "ВЫКЛЮЧЕН" : "ВКЛЮЧЕН";
            } else {
                _hook?.Dispose();
                button1.Text = "ЗАПУСКЕМ ФСБ";
            }
            isWorking = !isWorking;
        }

        private void _hook_KeyPressed(object sender, KeyPressEventArgs e) {
            if (!coolDown) {
                coolDown = true;
                micro.AudioEndpointVolume.Mute = !micro.AudioEndpointVolume.Mute;
                Task.Factory.StartNew(() => {
                    Thread.Sleep(100);
                    coolDown = false;
                });
            }
            button1.Text = micro.AudioEndpointVolume.Mute ? "ВЫКЛЮЧЕН" : "ВКЛЮЧЕН";
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e) {
            File.WriteAllLines($@"{Environment.CurrentDirectory}\config.xml", new[] { microName, keyCode.ToString(), textBox2.Text });
        }
    }


    public class Hook : IDisposable {
        #region Declare WinAPI functions
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, KeyboardHookProc callback, IntPtr hInstance, uint threadId);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, int wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hInstance);
        #endregion
        #region Constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_KEYDOWN = 0x0100;
        #endregion

        // код клавиши на которую ставим хук
        private int _key;
        public event KeyPressEventHandler KeyPressed;

        private delegate IntPtr KeyboardHookProc(int code, IntPtr wParam, IntPtr lParam);
        private KeyboardHookProc _proc;
        private IntPtr _hHook = IntPtr.Zero;

        public Hook(int keyCode) {
            _key = keyCode;
            _proc = HookProc;
        }

        public void SetHook() {
            var hInstance = LoadLibrary("User32");
            _hHook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hInstance, 0);
        }

        public void Dispose() {
            UnHook();
        }

        public void UnHook() {
            UnhookWindowsHookEx(_hHook);
        }

        private IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam) {
            if ((code >= 0 && wParam == (IntPtr)WH_KEYDOWN) && Marshal.ReadInt32(lParam) == _key) {

                // бросаем событие
                if (KeyPressed != null) {
                    KeyPressed(this, new KeyPressEventArgs(Convert.ToChar(code)));
                }
            }

            // пробрасываем хук дальше
            return CallNextHookEx(_hHook, code, (int)wParam, lParam);
        }
    }
}
