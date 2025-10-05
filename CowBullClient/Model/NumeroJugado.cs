using System.ComponentModel;

namespace CowBullClient.Model
{
    public class NumeroJugado:INotifyPropertyChanged
    {
        private string _numero;
        private int _toros;
        private int _vacas;

        public NumeroJugado() { }
        //public NumeroJugado(string numero, int toros, int vacas)
        //{
        //    Numero = numero;
        //    Toros = toros;
        //    Vacas = vacas;
        //}
        #region Properties


        public string Numero
        {
            get { return _numero; }
            set
            {
                _numero = value;
                RaisePropertyChanged("Numero");
            }
        }

        public int Toros
        {
            get { return _toros; }
            set
            {
                _toros = value;
                RaisePropertyChanged("Toros");
            }
        }

        public int Vacas
        {
            get { return _vacas; }
            set
            {
                _vacas = value;
                RaisePropertyChanged("Vacas");
            }
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
