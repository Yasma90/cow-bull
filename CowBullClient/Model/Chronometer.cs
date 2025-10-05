using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;

namespace CowBullClient.Model
{
    public class Chronometer : INotifyPropertyChanged
    {
        private enum AlarmState : int
        {
            Standby = 0,
            Arming,
            Armed,
            Triggered,
            Sounding
        }

        private cTimer _cStartTimer;
        private cTimer _cAlarmTimer;
        
        private Timer _time = new Timer();
        private TimeSpan span = new TimeSpan();

        public Chronometer()
        {
        }

        private void startLoading()
        {
            _cStartTimer = new cTimer(this);
            _cStartTimer.Start(100, 1000);
            //_cStartTimer.Tick += new cTimer.TickDelegate(_cStartTimer_Tick);
            //btnArm.Enabled = false;
        }

        private void _cStartTimer_Tick(object sender)
        {
            _iActiveTick++;
            if (_iActiveTick > 600)
            {
                finishedLoading();
            }
            else if (_iActiveTick > 100)
            {
                
            }
        }
        int _iActiveTick=0;
        private void finishedLoading()
        {
            if (_cStartTimer != null)
                _cStartTimer.Dispose();
            _iActiveTick = 0;
            //btnArm.Enabled = true;
            //stInfo.Items[0].Text = "Monitor is in Standby mode..";
            //_iTimerTick = 0;
            //if (chkVolume.Checked)
            //    volumeControl(false);
        }
     
        
        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class TimeSound:INotifyPropertyChanged
    {

        //private WaveOutPlayer m_Player;
        //private WaveFormat m_Format;
        private Stream m_AudioStream;
        //private MenuItem menuItemReset;
        //private MenuItem menuItemOpen;
        private string timerInput;
        //private TextBox timerInput;
        private Button StartButton;
        private Button ResetButton;
        //private NotifyIcon notifyIcon;
        //private ContextMenu notifyMenu;
        //private IContainer components;
        private Timer timerClock = new Timer();
        private int clockTime = 0;
        private int alarmTime = 0;

        public TimeSound()
        {
            //InitializeComponent();
            InitializeTimer();
            //InitializeSound();
            //InitializeNotifyMenu();
        }

        public string TimerInput
        {
            get { return timerInput; }
            set
            {
                timerInput = value;
                RaisePropertyChanged("TimerInput");
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            clockTime = 0;
            //inputToSeconds(timerInput.Content);
        }

        public void InitializeTimer()
        {
            timerClock.Elapsed += OnTimer;
            timerClock.Interval = 1000;
            timerClock.Enabled = true;
        }

        private void OnTimer(object sender, ElapsedEventArgs e)
        {
            try
            {
                clockTime++;
                TimerInput = secondsToTime(clockTime);
                //int countdown = alarmTime - clockTime;
                //if (alarmTime != 0)
                //{
                //    timerInput.Text = secondsToTime(countdown);
                //}

                ////Sound Alarm
                //if (clockTime == alarmTime)
                //{
                //    //PlaySound();
                //}
            }
            catch (Exception ex)
            {
                MessageBox.Show("OnTimer(): " + ex.Message);
            }
        }

        //private void inputToSeconds(object timerInput)
        //{
        //    try
        //    {
        //        string[] timeArray = new string[3];
        //        int minutes = 0;
        //        int hours = 0;
        //        int seconds = 0;
        //        int occurence = 0;
        //        int length = 0;

        //        occurence = timerInput.LastIndexOf(":");
        //        length = timerInput.Length;

        //        //Check for invalid input
        //        if (occurence == -1 || length != 8)
        //        {
        //            MessageBox.Show("Invalid Time Format.");
        //            //ResetButton_Click(null, null);
        //        }
        //        else
        //        {
        //            timeArray = timerInput.Split(':');

        //            seconds = Convert.ToInt32(timeArray[2]);
        //            minutes = Convert.ToInt32(timeArray[1]);
        //            hours = Convert.ToInt32(timeArray[0]);

        //            alarmTime += seconds;
        //            alarmTime += minutes*60;
        //            alarmTime += (hours*60)*60;
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        MessageBox.Show("inputToSeconds(): " + e.Message);
        //    }
        //}

        private void ResetButton_Click(object sender, EventArgs e)
        {
            try
            {
                clockTime = 0;
                alarmTime = 0;
                TimerInput = "00:00:00";
                StopSound();
            }
            catch (Exception ex)
            {
                MessageBox.Show("ResetButton_Click(): " + ex.Message);
            }
        }
        public string secondsToTime(int seconds)
        {
            int minutes = 0;
            int hours = 0;

            while (seconds >= 60)
            {
                minutes += 1;
                seconds -= 60;
            }
            while (minutes >= 60)
            {
                hours += 1;
                minutes -= 60;
            }

            string strHours = hours.ToString();
            string strMinutes = minutes.ToString();
            string strSeconds = seconds.ToString();

            if (strHours.Length < 2) strHours = "0" + strHours;
            if (strMinutes.Length < 2) strMinutes = "0" + strMinutes;
            if (strSeconds.Length < 2) strSeconds = "0" + strSeconds;

            return strHours + ":" + strMinutes + ":" + strSeconds;
        }

        private void CloseSound()
        {
            StopSound();
            if (m_AudioStream != null)
                try
                {
                    m_AudioStream.Close();
                }
                finally
                {
                    m_AudioStream = null;
                }
        }

        private void StopSound()
        {
            //if (m_Player != null)
            //    try
            //    {
            //        m_Player.Dispose();
            //    }
            //    finally
            //    {
            //        m_Player = null;
            //    }
        }

        private void PlaySound()
        {
            StopSound();
            if (m_AudioStream != null)
            {
                m_AudioStream.Position = 0;
                //m_Player = new WaveOutPlayer(-1, m_Format, 16384, 3, new BufferFillEventHandler(Filler));
            }
        }

        private void Filler(IntPtr data, int size)
        {
            byte[] b = new byte[size];
            if (m_AudioStream != null)
            {
                int pos = 0;
                while (pos < size)
                {
                    int toget = size - pos;
                    int got = m_AudioStream.Read(b, pos, toget);
                    if (got < toget)
                        m_AudioStream.Position = 0; // loop if the file ends
                    pos += got;
                }
            }
            else
            {
                for (int i = 0; i < b.Length; i++)
                    b[i] = 0;
            }
            Marshal.Copy(b, 0, data, size);
        }


        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
