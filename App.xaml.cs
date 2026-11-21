using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using VolumeHUD.CoreAudio;
using VolumeHUD.Hooks;
using VolumeHUD.Native;
using VolumeHUD.Views;
using Application = System.Windows.Application;

namespace VolumeHUD
{
    public partial class App : Application
    {
        private static EventWaitHandle? _eventWaitHandle;
        private HudWindow? _hudWindow;
        private MediaKeyHook? _mediaKeyHook;
        private NotifyIcon? _notifyIcon;

        public App()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string eventName = "Local\\Volu_ShowEvent_App";
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName, out bool isNewInstance);

            if (!isNewInstance)
            {
                // Another instance is already running: wake it up to show the HUD
                _eventWaitHandle.Set();
                Shutdown();
                return;
            }

            base.OnStartup(e);

            try
            {
                // Initialize HUD window
                _hudWindow = new HudWindow();
                _hudWindow.Show();
                var hwnd = new WindowInteropHelper(_hudWindow).EnsureHandle();

                // Initialize Audio & Hooks
                _mediaKeyHook = new MediaKeyHook();
                _mediaKeyHook.Start(hwnd);

                AudioManager.Instance.VolumeHudRequested += (vol, muted) =>
                {
                    _hudWindow.UpdateVolume(vol, muted);
                };

                // Listen for second-instance launches to trigger HUD
                ThreadPool.RegisterWaitForSingleObject(_eventWaitHandle, (state, timedOut) =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        _hudWindow.UpdateVolume(AudioManager.Instance.CurrentVolume, AudioManager.Instance.IsMuted);
                        SoundEngine.Play();
                    });
                }, null, -1, false);

                // Initialize System Tray
                SetupTrayIcon();

                // Show preview HUD on launch so user immediately sees it working
                _hudWindow.UpdateVolume(AudioManager.Instance.CurrentVolume, AudioManager.Instance.IsMuted);
                SoundEngine.Play();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при запуске Volu: {ex.Message}", "Volu", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = "Volu — Volume Overlay",
                Icon = CreateTrayIcon(),
                Visible = true
            };

            var contextMenu = new ContextMenuStrip
            {
                Renderer = new Windows11MenuRenderer(),
                ShowImageMargin = true,
                ShowCheckMargin = false,
                Font = new Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Regular),
                ForeColor = Color.FromArgb(240, 240, 245),
                BackColor = Color.FromArgb(28, 28, 30),
                DropShadowEnabled = true,
                Padding = new Padding(4, 6, 4, 6)
            };

            contextMenu.HandleCreated += (s, e) =>
            {
                int corner = 2; // DWMWCP_ROUND (Windows 11 rounded corners)
                Win32.DwmSetWindowAttribute(contextMenu.Handle, 33, ref corner, sizeof(int));
                int darkMode = 1;
                Win32.DwmSetWindowAttribute(contextMenu.Handle, 20, ref darkMode, sizeof(int));
            };

            var titleItem = new ToolStripMenuItem("Volu")
            {
                Enabled = false,
                Font = new Font("Segoe UI Semibold", 9.5f, System.Drawing.FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 160, 165),
                Padding = new Padding(8, 4, 8, 4)
            };
            contextMenu.Items.Add(titleItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            // Sound settings submenu
            var soundSubMenu = new ToolStripMenuItem("Выбор звука")
            {
                ForeColor = Color.FromArgb(240, 240, 245),
                Padding = new Padding(8, 4, 8, 4)
            };

            var woodClickItem = new ToolStripMenuItem("Wood Click")
            {
                Checked = SoundEngine.IsEnabled && SoundEngine.CurrentSoundName == "WoodClick",
                ForeColor = Color.FromArgb(240, 240, 245),
                Padding = new Padding(8, 4, 8, 4)
            };

            var tactileClickItem = new ToolStripMenuItem("Tactile Click")
            {
                Checked = SoundEngine.IsEnabled && SoundEngine.CurrentSoundName == "TactileClick",
                ForeColor = Color.FromArgb(240, 240, 245),
                Padding = new Padding(8, 4, 8, 4)
            };

            var muteSoundItem = new ToolStripMenuItem("Без звука")
            {
                Checked = !SoundEngine.IsEnabled,
                ForeColor = Color.FromArgb(240, 240, 245),
                Padding = new Padding(8, 4, 8, 4)
            };

            void UpdateSoundChecks()
            {
                woodClickItem.Checked = SoundEngine.IsEnabled && SoundEngine.CurrentSoundName == "WoodClick";
                tactileClickItem.Checked = SoundEngine.IsEnabled && SoundEngine.CurrentSoundName == "TactileClick";
                muteSoundItem.Checked = !SoundEngine.IsEnabled;
            }

            woodClickItem.Click += (s, e) =>
            {
                SoundEngine.SelectWoodClick();
                UpdateSoundChecks();
                SoundEngine.Play();
            };

            tactileClickItem.Click += (s, e) =>
            {
                SoundEngine.SelectTactileClick();
                UpdateSoundChecks();
                SoundEngine.Play();
            };

            muteSoundItem.Click += (s, e) =>
            {
                SoundEngine.DisableSound();
                UpdateSoundChecks();
            };

            soundSubMenu.DropDownItems.Add(woodClickItem);
            soundSubMenu.DropDownItems.Add(tactileClickItem);
            soundSubMenu.DropDownItems.Add(new ToolStripSeparator());
            soundSubMenu.DropDownItems.Add(muteSoundItem);

            contextMenu.Items.Add(soundSubMenu);

            var openSoundsItem = new ToolStripMenuItem("Открыть папку со звуками...")
            {
                ForeColor = Color.FromArgb(240, 240, 245),
                Padding = new Padding(8, 4, 8, 4)
            };
            openSoundsItem.Click += (s, e) =>
            {
                try
                {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds");
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    Process.Start("explorer.exe", dir);
                }
                catch { }
            };
            contextMenu.Items.Add(openSoundsItem);

            contextMenu.Opening += (s, e) =>
            {
                UpdateSoundChecks();
                SoundEngine.LoadSound();
            };

            // Startup
            var startupItem = new ToolStripMenuItem("Запускать вместе с Windows")
            {
                Checked = StartupManager.IsRunAtStartup(),
                CheckOnClick = true,
                ForeColor = Color.FromArgb(240, 240, 245),
                Padding = new Padding(8, 4, 8, 4)
            };
            startupItem.CheckedChanged += (s, e) =>
            {
                StartupManager.SetRunAtStartup(startupItem.Checked);
            };
            contextMenu.Items.Add(startupItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            var exitItem = new ToolStripMenuItem("Выход")
            {
                ForeColor = Color.FromArgb(255, 120, 120),
                Padding = new Padding(8, 4, 8, 4)
            };
            exitItem.Click += (s, e) => Shutdown();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Clicking tray icon shows HUD
            _notifyIcon.Click += (s, e) =>
            {
                if (e is MouseEventArgs me && me.Button == MouseButtons.Left)
                {
                    _hudWindow?.UpdateVolume(AudioManager.Instance.CurrentVolume, AudioManager.Instance.IsMuted);
                    SoundEngine.Play();
                }
            };
        }

        private static Icon CreateTrayIcon()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string trayPath = Path.Combine(baseDir, "tray.ico");
                if (File.Exists(trayPath))
                {
                    return new Icon(trayPath, SystemInformation.SmallIconSize);
                }
                string appPath = Path.Combine(baseDir, "app.ico");
                if (File.Exists(appPath))
                {
                    return new Icon(appPath, SystemInformation.SmallIconSize);
                }
            }
            catch { }

            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                using var brush = new SolidBrush(Color.White);
                using var pen = new Pen(Color.White, 2.0f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                var points = new PointF[]
                {
                    new(5, 11),
                    new(10, 11),
                    new(17, 5),
                    new(17, 27),
                    new(10, 21),
                    new(5, 21)
                };
                g.FillPolygon(brush, points);

                g.DrawArc(pen, 13, 10, 12, 12, -40, 80);
                g.DrawArc(pen, 14, 6, 18, 20, -40, 80);
            }

            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mediaKeyHook?.Dispose();
            AudioManager.Instance.Dispose();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            _eventWaitHandle?.Dispose();

            base.OnExit(e);
        }
    }
}
