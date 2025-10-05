using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace CowBullClient.Model
{
    public class CowBulls//:INotifyPropertyChanged
    {
        //protected NumeroJugado[] arregloNumerosJugados;
        private ObservableCollection<NumeroJugado> _listNumbersJugados = null; 
        private string numeroEncontrar = "1234";
        protected int CANTIDAD_INTENTOS_POSIBLES = 10;
        public const int ES_GANADOR = 1;
        public const int ES_PERDEDOR = 2;
        public const int CONTINUA_JUEGO = 3;
        protected int cantidadJugadasRealizadas;
        protected int CantidadCifrasNumeroEncontrar = 4;

        public CowBulls(string aNumeroEncontrar)
        {
            NumeroEncontrar = aNumeroEncontrar;
            //CantidadCifrasNumeroEncontrar = aNumeroEncontrar.Length;
            cantidadJugadasRealizadas = 0;
            _listNumbersJugados = new ObservableCollection<NumeroJugado>();
            //arregloNumerosJugados = new NumeroJugado[CANTIDAD_INTENTOS_POSIBLES];

            //Log.v("Numero a Encontrar", numeroEncontrar);
        }

        public CowBulls()
        {
            cantidadJugadasRealizadas = 0;
            _listNumbersJugados = new ObservableCollection<NumeroJugado>();
        }

        public ObservableCollection<NumeroJugado> ListNumbersJugados
        {
            get { return _listNumbersJugados; }
            set { _listNumbersJugados = value; }
        }

        public int GetCantidadCifrasNumero()
        {
            return CantidadCifrasNumeroEncontrar;
        }
        
        public ObservableCollection<NumeroJugado> GetListNumbersJugados()
        {
            return _listNumbersJugados;
        }

        public int GetCantidadJugadasRealizadas()
        {
            return ListNumbersJugados.Count; //cantidadJugadasRealizadas;
        }

        public string GetNumeroEncontrar
        {
            get { return numeroEncontrar; }
            set { numeroEncontrar = value; }
        }

        public string NumeroEncontrar
        {
            get { return numeroEncontrar; }
            set { numeroEncontrar = value; }
        }

        public NumeroJugado GetUltimoNumeroJugado()
        {
            return _listNumbersJugados[_listNumbersJugados.Count - 1];
        }

        public bool InArray(string aArreglo, char aElemento) //static
        {
            //for (int i = 0; i < aArreglo.Length; i++)
            //{
            //    if (aArreglo[i] == aElemento)
            //    {
            //        return true;
            //    }
            //}return false;
            return aArreglo.Any(t => t == aElemento);
        }

        public bool FueEncontradoNumero()
        {
            return cantidadJugadasRealizadas != 0 &&
                   _listNumbersJugados[_listNumbersJugados.Count - 1].Toros == CantidadCifrasNumeroEncontrar;
        }

        protected void ComprobarCertezaJugada(string aNumeroUsuario)
        {
            NumeroJugado numeroActual;

            if (aNumeroUsuario != numeroEncontrar)
            {
                int toros = 0, vacas = 0;

                for (int i = 0; i < CantidadCifrasNumeroEncontrar; i++)
                {
                    var numEncont = numeroEncontrar[i];
                    var numAdiv = aNumeroUsuario[i];
                    if (numEncont == numAdiv) //Todo:all array say chartin(i)
                    {
                        toros++;
                    }
                    else if (InArray(numeroEncontrar, numAdiv))
                    {
                        vacas++;
                    }
                }
                int numero = cantidadJugadasRealizadas + 1;
                var numeroJugada = cantidadJugadasRealizadas < 9 ? "0" + numero + " - " : numero + " - ";
                numeroActual = new NumeroJugado
                {
                    Numero = numeroJugada + aNumeroUsuario,
                    Toros = toros,
                    Vacas = vacas
                };
                //_listNumbersJugados = new List<NumeroJugado>();
            }
            else
            {
                var numero = cantidadJugadasRealizadas + 1;
                var numeroJugada = cantidadJugadasRealizadas < 9 ? "0" + numero + " - " : numero + " - ";
                numeroActual = new NumeroJugado
                {
                    Numero = numeroJugada + aNumeroUsuario,
                    Toros = 4,
                    Vacas = 0
                };
            }
            _listNumbersJugados.Add(numeroActual); //[cantidadJugadasRealizadas] = numeroActual;
            cantidadJugadasRealizadas++;
        }

        protected void CertezaJugada(NumeroJugado numeroAnswer)
        {
            NumeroJugado numeroActual;
            var numero = cantidadJugadasRealizadas + 1;
            var numeroJugada = cantidadJugadasRealizadas < 9 ? "0" + numero + " - " : numero + " - ";
            
            //numeroActual = numeroAnswer;
            numeroActual = new NumeroJugado
            {
                Numero = numeroJugada + numeroAnswer.Numero,
                Toros = numeroAnswer.Toros,
                Vacas = numeroAnswer.Vacas
            };

            _listNumbersJugados.Add(numeroActual); //[cantidadJugadasRealizadas] = numeroActual;
            cantidadJugadasRealizadas++;
        }
        public bool EsValidaJugada(string aNumeroUsuario)
        {
            return aNumeroUsuario.Length == CantidadCifrasNumeroEncontrar && !RepiteDigit(aNumeroUsuario);
        }

        /// <summary>
        /// Return true if repet numbers
        /// </summary>
        /// <param name="numUsuario"></param>
        /// <returns></returns>
        private bool RepiteDigit(string numUsuario)
        {
            foreach (char num in numUsuario)
            {
                var count = 0;
                foreach (char n in numUsuario)
                {
                    if (n == num)
                        count++;
                    if (count > 1)
                        return true;
                }
            }
            return false;
        }

        #region INotifyPropertyChanged Members

        //public event PropertyChangedEventHandler PropertyChanged;

        //public void RaisePropertyChanged(string propertyName)
        //{
        //    if (PropertyChanged != null)
        //        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        //}

        #endregion
    }

}
