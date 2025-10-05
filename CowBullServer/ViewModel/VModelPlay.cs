using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CowBullServer.Model;
using CowBullServer.View;
using Control =System.Windows.Controls.Control;

namespace CowBullServer.ViewModel
{
    public class VModelPlay: INotifyPropertyChanged
    {
        #region Field

        private TimeSound _timeSound = new TimeSound();
        private Control _vistaActual;
        private readonly CowBullVsPC _juegoVspc;

        private ObservableCollection<NumeroJugado> _listShow;
        
        //private VacasToros _juegVacaT;
        private bool _enableButton = false;
        #endregion

        #region Constructor
        public VModelPlay()
        {
            //_timeSound = new TimeSound();
            //_server=new ServerSocket();
            _juegoVspc = new CowBullVsPC();
            ListShow = new ObservableCollection<NumeroJugado>();
        }
        #endregion

        private RelayCommand _sendMsg;

        public ICommand SendMessage
        {
            get
            {
                return _sendMsg ?? (_sendMsg = new RelayCommand(
                    x =>
                    {
                        JuegoVspc.GenerarNumeroAleatorio(4);
                        JuegoVspc.Server.Send_Msg(JuegoVspc.NumeroEncontrar);
                    }
                    ));
            }
        }

        private RelayCommand _listenServ;

        public ICommand ListenServer
        {
            get { return _listenServ ?? (_listenServ = new RelayCommand(x => JuegoVspc.Server.Listen_Click())); }
        }

        #region Properties

        public Control VistaActual
        {
            get { return _vistaActual; }
            set
            {
                _vistaActual = value;
                RaisePropertyChanged("VistaActual");
            }
        }

        public bool IsEnableTime
        {
            get
            {
                if (ListShow.Count == 0)
                    return false;
                //TimeSound.InitializeTimer();
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
                if (_tboxSendNumber != value)
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
                if (_listShow != value)
                    _listShow = value;
                RaisePropertyChanged("ListShow");
            }
        }

        public bool EnableButton
        {
            get { return _enableButton; }
            set
            {
                if(_enableButton!=value)
                    _enableButton = value;
                RaisePropertyChanged("EnableButton");
            }
        }

        #endregion

        #region Commands

        private RelayCommand _btnSendNumber;

        public ICommand BtnSendCommand
        {
            get
            {
                return _btnSendNumber ?? (_btnSendNumber = new RelayCommand(x =>
                {
                    JuegoVspc.Jugada(TBoxSendNumber);
                    //Todo:Ver para poder capturarla ....

                    ListShow = JuegoVspc.ListNumbersJugados;
                    var count = ListShow.Count - 1;
                    var lis = ListShow[count];
                    lis.Numero = lis.Numero;
                    lis.Toros = lis.Toros;
                    lis.Vacas = lis.Vacas;

                    switch (JuegoVspc.ResultadosJuego())
                    {
                        case Gamer.EsGanador:
                            Ganador();
                            break;

                        case Gamer.EsPerdedor:
                            Perdedor();
                            break;

                        case Gamer.ContinuaJuego:
                            if (JuegoVspc.GetCantidadJugadasRealizadas() == 9)
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

        private RelayCommand _cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    _cancelCommand = new RelayCommand(x =>
                    {
                        VistaActual = null;
                        
                    });
                }
                return _cancelCommand;
            }
        }

        public CowBullVsPC JuegoVspc => _juegoVspc;

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
