using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CowBullServer.Properties;

namespace CowBullServer.Model
{
    class Sound
    {
        private SoundPlayer _cAlarmSound;
        private SoundPlayer _cWarningSound;
        Logging _log=new Logging();

        public Sound()
        {
        }

        private void playWarning()
        {
            _cWarningSound.Play();
        }
        private void playAlarm(bool looping)
        {
            if (looping)
                _cAlarmSound.PlayLooping();
            else
                _cAlarmSound.Play();
        }
        private void loadAlarmGanad()
        {
            if (_cAlarmSound != null)
                _cAlarmSound.Dispose();
            var uri = Application.Current + "Sound/sonido_ganador.mp3";
            _cAlarmSound = new SoundPlayer(uri);
            _cAlarmSound.LoadAsync();
        }
        void LoadWarningPerdedor()
        {
            if (_cWarningSound != null)
                _cWarningSound.Dispose();
            var uri = Application.Current + "Sound/sonido_perdedor.mp3";
            _cWarningSound = new SoundPlayer(uri);
            _cWarningSound.Load();
        }
        
        private void volumeControl(bool max)
        {
            uint vol;
            if (max)
                vol = (255 & 0x00ff) | (255 << 8);
            else
                vol = (64 & 0x00ff) | (64 << 8);

            Mixer mx = new Mixer();
            mx.OpenMixer();
            mx.SetVolume(vol);
            mx.CloseMixer();
        }


        private AlarmState _eAlarmState = AlarmState.Standby;
        private int _iTimerTick = 0;
        private int _iActiveTick = 0;

        private void soundAlarm()
        {
            if (true)
                volumeControl(true);
            _eAlarmState = AlarmState.Sounding;
            if (true)//chkSoundAlarm.Checked
                _cAlarmSound.PlayLooping();
            _iActiveTick = 0;
            _iTimerTick = 0;
            //if (!_bSounded)
            //{
            //    _bSounded = true;
            //    if (txtApplication.Text.Contains(".") && (txtApplication.Text.Length > 4))
            //    {
            //        executeProgram(txtApplication.Text);
            //    }
            //}
        }

        private void saveSettings()
        {
            // Properties.Settings.Default.SettingBestGame = "Yasmany";// chkOptions.Checked;
            Settings.Instance.Save();
        }
    }
}

internal enum AlarmState
{
    Standby = 0,
    Arming,
    Armed,
    Triggered,
    Sounding
}        

