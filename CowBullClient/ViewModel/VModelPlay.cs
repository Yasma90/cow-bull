using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CowBullClient.Model;
using CowBullClient.View;
using Control = System.Windows.Controls.Control;

namespace CowBullClient.ViewModel
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
            ListShow = new ObservableCollection<NumeroJugado>();
        }
        #endregion

        private RelayCommand _connectServ;

        public ICommand ConnectCmd
        {
            get { return _connectServ ?? (_connectServ = new RelayCommand(x => juegoVSPC.Client.Connect_Server())); }
        }

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
       
        public ObservableCollection<NumeroJugado> ListShow
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

        private ObservableCollection<NumeroJugado> _listShow;
        public ICommand BtnSendCommand
        {
            get
            {
                return _btnSendNumber ?? (_btnSendNumber = new RelayCommand(x =>
                {
                    juegoVSPC.Jugada(TBoxSendNumber);
                    //Todo:Ver para poder capturarla ....
                    
                    ListShow = juegoVSPC.ListNumbersJugados; 
                    var count = ListShow.Count-1;
                    var lis = ListShow[count];
                    lis.Numero = lis.Numero;
                    lis.Toros = lis.Toros;
                    lis.Vacas = lis.Vacas;

                    switch (juegoVSPC.ResultadosJuego())
                    {
                        case CowBulls.ES_GANADOR:
                            Ganador();
                            break;

                        case CowBulls.ES_PERDEDOR:
                            Perdedor();
                            break;

                        case CowBulls.CONTINUA_JUEGO:
                            if (juegoVSPC.GetCantidadJugadasRealizadas() == 9)
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
