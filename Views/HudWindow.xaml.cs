using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using VolumeHUD.Native;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace VolumeHUD.Views
{
    public partial class HudWindow : Window
    {
        private readonly List<Border> _blocks = new();
        private readonly DispatcherTimer _hideTimer;

        // Classic macOS HUD: White filled segments over a continuous dark track
        private static readonly Brush BorderColorBrush = new SolidColorBrush(Color.FromRgb(28, 30, 36)); // #1C1E24
        private static readonly Brush ActiveBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // Pure White
        private static readonly Brush InactiveBrush = System.Windows.Media.Brushes.Transparent; // Shows dark track

        public HudWindow()
        {
            InitializeComponent();

            // Create 16 segmented blocks separated by exact 1px hairline border strokes
            for (int i = 0; i < 16; i++)
            {
                var block = new Border
                {
                    BorderBrush = BorderColorBrush,
                    BorderThickness = (i < 15) ? new Thickness(0, 0, 1, 0) : new Thickness(0),
                    Background = InactiveBrush,
                    SnapsToDevicePixels = true
                };
                Grid.SetColumn(block, i);
                _blocks.Add(block);
                SegmentsGrid.Children.Add(block);
            }

            // Hide timer (1.3 seconds)
            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1300)
            };
            _hideTimer.Tick += OnHideTimerTick;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32.MakeClickThroughAndNoActivate(hwnd);

            // Explicitly disable DWM non-client rendering and window frame rounding
            int ncPolicy = Win32.DWMNCRP_DISABLED;
            Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_NCRENDERING_POLICY, ref ncPolicy, sizeof(int));

            int cornerPref = Win32.DWMWCP_DONOTROUND;
            Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));
        }

        public void UpdateVolume(float volume, bool isMuted)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateVolume(volume, isMuted)));
                return;
            }

            CenterOnActiveScreen();

            // Update speaker icon & waves according to classic macOS
            if (isMuted)
            {
                // In macOS mute state, speaker body & 3 waves are shown with diagonal strike-through slash
                Wave1.Visibility = Visibility.Visible;
                Wave2.Visibility = Visibility.Visible;
                Wave3.Visibility = Visibility.Visible;
                MuteSlash.Visibility = Visibility.Visible;
            }
            else
            {
                MuteSlash.Visibility = Visibility.Collapsed;
                Wave1.Visibility = volume > 0.01f ? Visibility.Visible : Visibility.Collapsed;
                Wave2.Visibility = volume > 0.33f ? Visibility.Visible : Visibility.Collapsed;
                Wave3.Visibility = volume > 0.66f ? Visibility.Visible : Visibility.Collapsed;
            }

            // Update 16 blocks (all transparent showing dark track when muted)
            int filledCount = isMuted ? 0 : (int)Math.Round(volume * 16.0f);
            for (int i = 0; i < 16; i++)
            {
                _blocks[i].Background = (i < filledCount) ? ActiveBrush : InactiveBrush;
            }

            // Show window and ensure it is brought to the top over games
            Show();

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
            }

            // Animate fade-in
            BeginAnimation(OpacityProperty, null);
            Opacity = 1.0;

            // Reset hide timer
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        private void CenterOnActiveScreen()
        {
            try
            {
                // Find screen of foreground window (e.g. fullscreen game)
                IntPtr fgHwnd = Win32.GetForegroundWindow();
                var screen = (fgHwnd != IntPtr.Zero)
                    ? System.Windows.Forms.Screen.FromHandle(fgHwnd)
                    : System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];

                var bounds = screen.Bounds;
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                double screenWidth = bounds.Width / dpiX;
                double screenHeight = bounds.Height / dpiY;
                double screenLeft = bounds.Left / dpiX;
                double screenTop = bounds.Top / dpiY;

                Left = screenLeft + (screenWidth - Width) / 2.0;
                Top = screenTop + (screenHeight - Height) / 2.0;
            }
            catch
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2.0;
                Top = (SystemParameters.PrimaryScreenHeight - Height) / 2.0;
            }
        }

        private void OnHideTimerTick(object? sender, EventArgs e)
        {
            _hideTimer.Stop();

            var fadeOut = new DoubleAnimation
            {
                From = Opacity,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            fadeOut.Completed += (s, args) =>
            {
                if (Opacity == 0.0)
                {
                    Hide();
                }
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
