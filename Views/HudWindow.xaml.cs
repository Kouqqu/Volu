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

        private static readonly Brush ActiveBrush = new SolidColorBrush(Color.FromRgb(245, 245, 247));
        private static readonly Brush InactiveBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));

        public HudWindow()
        {
            InitializeComponent();

            // Create 16 blocks for the segmented volume meter
            for (int i = 0; i < 16; i++)
            {
                var block = new Border
                {
                    Height = 8,
                    CornerRadius = new CornerRadius(1.5),
                    Margin = new Thickness(1.2, 0, 1.2, 0),
                    Background = InactiveBrush,
                    SnapsToDevicePixels = true
                };
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

            // Update speaker icon
            if (isMuted)
            {
                Wave1.Visibility = Visibility.Collapsed;
                Wave2.Visibility = Visibility.Collapsed;
                Wave3.Visibility = Visibility.Collapsed;
                MuteCross.Visibility = Visibility.Visible;
            }
            else
            {
                MuteCross.Visibility = Visibility.Collapsed;
                Wave1.Visibility = volume > 0.01f ? Visibility.Visible : Visibility.Collapsed;
                Wave2.Visibility = volume > 0.33f ? Visibility.Visible : Visibility.Collapsed;
                Wave3.Visibility = volume > 0.66f ? Visibility.Visible : Visibility.Collapsed;
            }

            // Update 16 blocks
            int filledCount = isMuted ? 0 : (int)Math.Round(volume * 16.0f);
            for (int i = 0; i < 16; i++)
            {
                _blocks[i].Background = (i < filledCount) ? ActiveBrush : InactiveBrush;
            }

            // Show window
            Show();

            // Clear previous animation and snap to full opacity
            BeginAnimation(OpacityProperty, null);
            Opacity = 1.0;

            // Reset hide timer
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        private void OnHideTimerTick(object? sender, EventArgs e)
        {
            _hideTimer.Stop();

            // Smooth fade out
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (s, ev) =>
            {
                BeginAnimation(OpacityProperty, null);
                Opacity = 0.0;
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void CenterOnActiveScreen()
        {
            try
            {
                var cursor = System.Windows.Forms.Cursor.Position;
                var screen = System.Windows.Forms.Screen.FromPoint(cursor);

                var dpi = VisualTreeHelper.GetDpi(this);
                double dpiX = dpi.DpiScaleX;
                double dpiY = dpi.DpiScaleY;

                double screenLeft = screen.Bounds.Left / dpiX;
                double screenTop = screen.Bounds.Top / dpiY;
                double screenWidth = screen.Bounds.Width / dpiX;
                double screenHeight = screen.Bounds.Height / dpiY;

                // Dead center on the active monitor
                Left = screenLeft + (screenWidth - Width) / 2.0;
                Top = screenTop + (screenHeight - Height) / 2.0;
            }
            catch
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2.0;
                Top = (SystemParameters.PrimaryScreenHeight - Height) / 2.0;
            }
        }
    }
}
