using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CowBull.Model;
using CowBull.View;
using Control = System.Windows.Controls.Control;

namespace CowBull.ViewModel
{
    public class VModelPlay: INotifyPropertyChanged
    {
        #region Field

        private readonly CowBullVsPC juegoVSPC;
        private CowBulls _juegVacaT;
        private bool _enableButton = false;
        private TimeSound _timeSound=new TimeSound();

        #endregion
        #region Constructor
        public VModelPlay()
        {
            //_timeSound = new TimeSound();
            juegoVSPC = new CowBullVsPC();
            ListShow = new ObservableCollection<PlayedNumber>();
        }
        #endregion
        
        #region Properties

        public bool IsEnableTime
        {
            get
            {
                if (ListShow.Count == 0) return false;
                TimeSound.InitializeTimer();
                RaisePropertyChanged("IsEnableTime");
                return true;
            }
        }
        
        public TimeSound TimeSound
        {
            get { return _timeSound; }
            set
            {
                _timeSound = value;
                RaisePropertyChanged("TimeSound");
            }
        }

        private string _tboxSendNumber;
        public string TBoxSendNumber
        {
            get { return _tboxSendNumber; }
            set
            {
                if (_tboxSendNumber == value) return;
                _tboxSendNumber = value;
                if (TBoxSendNumber != "")
                    EnableButton = true;
                RaisePropertyChanged("TBoxSendNumber");
            }
        }
       
        public ObservableCollection<PlayedNumber> ListShow
        {
            get { return _listShow; }
            set
            {
                _listShow = value;
                RaisePropertyChanged("ListShow");
            }
        }

        public bool EnableButton
        {
            get { return _enableButton; }
            set
            {
                _enableButton = value;
                RaisePropertyChanged("EnableButton");
            }
        }

        #endregion

        #region Commands

        
        private RelayCommand _btnSendNumber;

        private ObservableCollection<PlayedNumber> _listShow;
        public ICommand BtnSendCommand
        {
            get
            {
                return _btnSendNumber ?? (_btnSendNumber = new RelayCommand(x =>
                {
                    juegoVSPC.PlayMove(TBoxSendNumber);
                    //Todo:Ver para poder capturarla ....
                    
                    ListShow = juegoVSPC.PlayedNumbers; 
                    var count = ListShow.Count-1;
                    var lis = ListShow[count];
                    lis.Number = lis.Number;
                    lis.Bulls = lis.Bulls;
                    lis.Cows = lis.Cows;

                    switch (juegoVSPC.GetGameResult())
                    {
                        case GameResult.Winner:
                            Ganador();
                            break;

                        case GameResult.Loser:
                            Perdedor();
                            break;

                        case GameResult.GameContinues:
                            if (juegoVSPC.GetPlayedMovesCount() == 9)
                            {
                                // Reproduciendo continuamente sonido de la penÃºltima jugada
                                //reproducirPenultimaJugada();
                            }
                            break;
                    }
                    TBoxSendNumber = "";
                    EnableButton = false;
                }));
            }
        }
        
        #endregion

        #region Privates Method
        private void Perdedor()
        {
            MessageBox.Show("Game Over", "Information", MessageBoxButton.OK, MessageBoxImage.Stop,
                MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
        }
        private void Ganador()
        {
            MessageBox.Show("Congratulation", "Information", MessageBoxButton.OK, MessageBoxImage.Information,
                MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
        }

        #endregion

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
