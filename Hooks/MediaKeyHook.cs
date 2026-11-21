using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;
using VolumeHUD.CoreAudio;
using VolumeHUD.Native;

namespace VolumeHUD.Hooks
{
    public class MediaKeyHook : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private static IntPtr _hookId = IntPtr.Zero;
        private static Win32.LowLevelKeyboardProc? _proc;
        private IntPtr _hwnd = IntPtr.Zero;
        private DispatcherTimer? _watchdogTimer;

        private const int HOTKEY_ID_UP = 9001;
        private const int HOTKEY_ID_DOWN = 9002;
        private const int HOTKEY_ID_MUTE = 9003;

        public void Start(IntPtr hwnd)
        {
            _hwnd = hwnd;

            EnsureHookInstalled();

            // Register Global Hotkeys as secondary fallback: Ctrl + Alt + Up/Down/V
            if (_hwnd != IntPtr.Zero)
            {
                Win32.RegisterHotKey(_hwnd, HOTKEY_ID_UP, Win32.MOD_CONTROL | Win32.MOD_ALT, Win32.VK_UP);
                Win32.RegisterHotKey(_hwnd, HOTKEY_ID_DOWN, Win32.MOD_CONTROL | Win32.MOD_ALT, Win32.VK_DOWN);
                Win32.RegisterHotKey(_hwnd, HOTKEY_ID_MUTE, Win32.MOD_CONTROL | Win32.MOD_ALT, 0x56); // Key 'V'

                ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
            }

            // Watchdog: verify hook is active every 2 seconds; reinstall if dropped by OS
            _watchdogTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _watchdogTimer.Tick += (s, e) => EnsureHookInstalled();
            _watchdogTimer.Start();
        }

        private static void EnsureHookInstalled()
        {
            if (_hookId == IntPtr.Zero)
            {
                _proc = HookCallback;
                // For WH_KEYBOARD_LL in the calling process, hMod MUST be IntPtr.Zero on 64-bit Windows
                _hookId = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
            }
        }

        public void Stop()
        {
            if (_watchdogTimer != null)
            {
                _watchdogTimer.Stop();
                _watchdogTimer = null;
            }

            if (_hookId != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                _proc = null;
            }

            if (_hwnd != IntPtr.Zero)
            {
                ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;
                Win32.UnregisterHotKey(_hwnd, HOTKEY_ID_UP);
                Win32.UnregisterHotKey(_hwnd, HOTKEY_ID_DOWN);
                Win32.UnregisterHotKey(_hwnd, HOTKEY_ID_MUTE);
                _hwnd = IntPtr.Zero;
            }
        }

        private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == Win32.WM_HOTKEY)
            {
                int id = msg.wParam.ToInt32();
                if (id == HOTKEY_ID_UP)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        AudioManager.Instance.VolumeUp();
                        SoundEngine.Play();
                    });
                    handled = true;
                }
                else if (id == HOTKEY_ID_DOWN)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        AudioManager.Instance.VolumeDown();
                        SoundEngine.Play();
                    });
                    handled = true;
                }
                else if (id == HOTKEY_ID_MUTE)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        AudioManager.Instance.ToggleMute();
                    });
                    handled = true;
                }
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                bool isCtrl = (Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) != 0;
                bool isAlt = (Win32.GetAsyncKeyState(Win32.VK_MENU) & 0x8000) != 0;

                bool isVolUp = kbd.vkCode == Win32.VK_VOLUME_UP || (isCtrl && isAlt && kbd.vkCode == Win32.VK_UP);
                bool isVolDown = kbd.vkCode == Win32.VK_VOLUME_DOWN || (isCtrl && isAlt && kbd.vkCode == Win32.VK_DOWN);
                // Mute: physical VK_VOLUME_MUTE or Ctrl+Alt+V
                bool isMute = kbd.vkCode == Win32.VK_VOLUME_MUTE || (isCtrl && isAlt && kbd.vkCode == 0x56);

                if (isVolUp || isVolDown || isMute)
                {
                    if (wParam == (IntPtr)Win32.WM_KEYDOWN || wParam == (IntPtr)Win32.WM_SYSKEYDOWN)
                    {
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            try
                            {
                                if (isVolUp)
                                {
                                    AudioManager.Instance.VolumeUp();
                                    SoundEngine.Play();
                                }
                                else if (isVolDown)
                                {
                                    AudioManager.Instance.VolumeDown();
                                    SoundEngine.Play();
                                }
                                else if (isMute)
                                {
                                    AudioManager.Instance.ToggleMute();
                                }
                            }
                            catch { }
                        });
                    }

                    // Always suppress both KEYDOWN and KEYUP for volume keys so Windows Shell NEVER receives them
                    return (IntPtr)1;
                }
            }

            return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
