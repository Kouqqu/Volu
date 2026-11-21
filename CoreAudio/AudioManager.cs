using System;
using NAudio.CoreAudioApi;

namespace VolumeHUD.CoreAudio
{
    public class AudioManager : IDisposable
    {
        private static AudioManager? _instance;
        public static AudioManager Instance => _instance ??= new AudioManager();

        private readonly MMDeviceEnumerator _deviceEnumerator;
        private MMDevice? _defaultDevice;
        private AudioEndpointVolume? _endpointVolume;

        public event Action<float, bool>? VolumeChanged;

        public const float Step = 1.0f / 16.0f; // 6.25% matches 16 blocks exactly

        public AudioManager()
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            InitializeDefaultDevice();
        }

        private void InitializeDefaultDevice()
        {
            try
            {
                if (_endpointVolume != null)
                {
                    _endpointVolume.OnVolumeNotification -= OnVolumeNotification;
                }

                _defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                _endpointVolume = _defaultDevice.AudioEndpointVolume;
                _endpointVolume.OnVolumeNotification += OnVolumeNotification;
            }
            catch
            {
                // Audio device might be unavailable temporarily
            }
        }

        private void OnVolumeNotification(AudioVolumeNotificationData data)
        {
            VolumeChanged?.Invoke(data.MasterVolume, data.Muted);
        }

        public float CurrentVolume
        {
            get
            {
                try
                {
                    EnsureDevice();
                    return _endpointVolume?.MasterVolumeLevelScalar ?? 0f;
                }
                catch
                {
                    return 0f;
                }
            }
        }

        public bool IsMuted
        {
            get
            {
                try
                {
                    EnsureDevice();
                    return _endpointVolume?.Mute ?? false;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void VolumeUp()
        {
            try
            {
                EnsureDevice();
                if (_endpointVolume == null) return;

                if (_endpointVolume.Mute)
                {
                    _endpointVolume.Mute = false;
                }

                float current = _endpointVolume.MasterVolumeLevelScalar;
                int currentBlock = (int)MathF.Round(current * 16.0f);
                int nextBlock = Math.Min(16, currentBlock + 1);
                float target = nextBlock / 16.0f;

                _endpointVolume.MasterVolumeLevelScalar = target;

                // If volume was already at maximum (target == current), WASAPI notification won't fire, so trigger HUD explicitly
                if (MathF.Abs(target - current) < 0.001f)
                {
                    VolumeChanged?.Invoke(target, false);
                }
            }
            catch
            {
                InitializeDefaultDevice();
            }
        }

        public void VolumeDown()
        {
            try
            {
                EnsureDevice();
                if (_endpointVolume == null) return;

                if (_endpointVolume.Mute)
                {
                    _endpointVolume.Mute = false;
                }

                float current = _endpointVolume.MasterVolumeLevelScalar;
                int currentBlock = (int)MathF.Round(current * 16.0f);
                int prevBlock = Math.Max(0, currentBlock - 1);
                float target = prevBlock / 16.0f;

                _endpointVolume.MasterVolumeLevelScalar = target;

                // If volume was already at minimum (target == current), WASAPI notification won't fire, so trigger HUD explicitly
                if (MathF.Abs(target - current) < 0.001f)
                {
                    VolumeChanged?.Invoke(target, false);
                }
            }
            catch
            {
                InitializeDefaultDevice();
            }
        }

        public void ToggleMute()
        {
            try
            {
                EnsureDevice();
                if (_endpointVolume == null) return;
                _endpointVolume.Mute = !_endpointVolume.Mute;
            }
            catch
            {
                InitializeDefaultDevice();
            }
        }

        public void SetVolume(float scalar)
        {
            try
            {
                EnsureDevice();
                if (_endpointVolume == null) return;
                _endpointVolume.MasterVolumeLevelScalar = Math.Clamp(scalar, 0f, 1f);
            }
            catch
            {
                InitializeDefaultDevice();
            }
        }

        private void EnsureDevice()
        {
            if (_endpointVolume == null || _defaultDevice == null)
            {
                InitializeDefaultDevice();
            }
        }

        public void Dispose()
        {
            if (_endpointVolume != null)
            {
                _endpointVolume.OnVolumeNotification -= OnVolumeNotification;
            }
            _deviceEnumerator.Dispose();
        }
    }
}
