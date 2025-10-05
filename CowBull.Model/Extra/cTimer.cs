using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Timers;

namespace CowBull.Model
{
    public class cTimer : IDisposable
    {
        #region Events
        public delegate void CompleteDelegate(object sender);
        public event CompleteDelegate Complete;
        public delegate void TickDelegate(object sender);
        public event TickDelegate Tick;
        #endregion

        #region Fields
        private bool _bCancelTimer;
        private bool _bIsReset;
        private long _lTickCounter;
        private long _lTickMaximum;
        private Timer _aTimer;
        #endregion

        #region Properties
        public bool Cancel
        {
            get { return _bCancelTimer; }
            set { _bCancelTimer = value; }
        }

        public bool Enabled
        {
            get { return _aTimer.Enabled; }
            set { _aTimer.Enabled = value; }
        }

        public double Interval
        {
            get { return _aTimer.Interval; }
            set { _aTimer.Interval = value; }
        }

        public long TickCount
        {
            get { return _lTickCounter; }
            set { _lTickCounter = value; }
        }

        public long TickMaximum
        {
            get { return _lTickMaximum; }
            set { _lTickMaximum = value; }
        }
        #endregion

        #region Constructor
        public cTimer(object sender)
        {
            TickCount = 0;
            TickMaximum = 1000;
            _aTimer = new Timer {SynchronizingObject = (ISynchronizeInvoke) sender};
            _aTimer.Elapsed += OnTimedEvent;
            Interval = 1000;

        }
        #endregion

        #region Methods
        private void Reset()
        {
            Enabled = false;
            TickCount = 0;
            Cancel = false;
        }

        public void Dispose()
        {
            Stop();
            _aTimer.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Start(double interval, long maximum)
        {
            Interval = interval;
            TickMaximum = maximum;
            Enabled = true;
        }

        public void Stop()
        {
            Reset();
        }
        #endregion

        #region Event Handlers
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if ((TickCount > TickMaximum && TickMaximum != -1) || Cancel)
            {
                Reset();
                if (Complete != null)
                    Complete(this);
            }
            else
            {
                if (TickMaximum != -1)
                    TickCount++;
                if (Tick != null)
                    Tick(this);
            }
        }
        #endregion
    }
}
