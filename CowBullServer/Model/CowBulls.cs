using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace CowBullServer.Model
{
    public class CowBulls //:INotifyPropertyChanged
    {
        #region Fields

        public ObservableCollection<NumeroJugado> ListNumbersJugados { get; set; }
        public String NumeroEncontrar { get; set; }
        protected int CANTIDAD_INTENTOS_POSIBLES = 10;
        protected int cantidadJugadasRealizadas;
        protected int CantidadCifrasNumeroEncontrar = 4;

        #endregion

        #region Constructor

        public CowBulls(String aNumeroEncontrar)
        {
            ListNumbersJugados = new ObservableCollection<NumeroJugado>();
            NumeroEncontrar = aNumeroEncontrar;
            //CantidadCifrasNumeroEncontrar = aNumeroEncontrar.Length;
            cantidadJugadasRealizadas = 0;
            //_listNumbersJugados = new List<NumeroJugado>();

            //Log.v("Numero a Encontrar", numeroEncontrar);
        }

        public CowBulls()
        {
            NumeroEncontrar = "1234";
            ListNumbersJugados = new ObservableCollection<NumeroJugado>();
            cantidadJugadasRealizadas = 0;
        }

        #endregion

        #region Methods

        protected void CertezaJugada(NumeroJugado numeroAnswer)
        {
            int numero = cantidadJugadasRealizadas + 1;
            string numeroJugada = cantidadJugadasRealizadas < 9 ? "0" + numero + " - " : numero + " - ";
            NumeroJugado numeroActual = numeroAnswer;
            //numeroActual = new NumeroJugado
            //{
            //    Numero = numeroJugada + numeroAnswer.Numero,
            //    Toros = numeroAnswer.Toros,
            //    Vacas = numeroAnswer.Vacas
            //};

            ListNumbersJugados.Add(numeroActual); //[cantidadJugadasRealizadas] = numeroActual;
            cantidadJugadasRealizadas++;
        }

        //public ObservableCollection<NumeroJugado> ListNumbersJugados
        //{
        //    get { return _listNumbersJugados; }
        //    set { _listNumbersJugados = value; }
        //}

        public int GetCantidadCifrasNumero()
        {
            return CantidadCifrasNumeroEncontrar;
        }

        public int GetCantidadJugadasRealizadas()
        {
            return ListNumbersJugados.Count; //cantidadJugadasRealizadas;
        }

        public NumeroJugado GetUltimoNumeroJugado()
        {
            return ListNumbersJugados[ListNumbersJugados.Count - 1];
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
                   ListNumbersJugados[ListNumbersJugados.Count - 1].Toros == CantidadCifrasNumeroEncontrar;
        }

        protected void ComprobarCertezaJugada(string aNumeroUsuario)
        {
            NumeroJugado numeroActual;

            if (aNumeroUsuario != NumeroEncontrar)
            {
                int toros = 0, vacas = 0;

                for (int i = 0; i < CantidadCifrasNumeroEncontrar; i++)
                {
                    var numEncont = NumeroEncontrar[i];
                    var numAdiv = aNumeroUsuario[i];
                    if (numEncont == numAdiv) //Todo:all array say chartin(i)
                    {
                        toros++;
                    }
                    else if (InArray(NumeroEncontrar, numAdiv))
                    {
                        vacas++;
                    }
                }
                int numero = cantidadJugadasRealizadas + 1;
                string numeroJugada = cantidadJugadasRealizadas < 9 ? "0" + numero + " - " : numero + " - ";
                numeroActual = new NumeroJugado
                {
                    Numero = numeroJugada + aNumeroUsuario,
                    Toros = toros,
                    Vacas = vacas
                };
                //ListNumbersJugados = new List<NumeroJugado>();
            }
            else
            {
                int numero = cantidadJugadasRealizadas + 1;
                string numeroJugada = cantidadJugadasRealizadas < 9 ? "0" + numero + " - " : numero + " - ";
                numeroActual = new NumeroJugado
                {
                    Numero = numeroJugada + aNumeroUsuario,
                    Toros = 4,
                    Vacas = 0
                };
            }
            ListNumbersJugados.Add(numeroActual);
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

        //bool repNum = false;
        private bool RepiteDigit(string numUsuario)
        {
            string number = numUsuario;
            foreach (char num in number)
            {
                number = number.Substring(1,number.Length);
                if (number.Contains(num))
                    return true;
                //var count = 0;
                //foreach (char n in numUsuario)
                //{
                //    if (n == num)
                //        count++;
                //    if (count > 1)
                //        repNum = true;
                //}
            }
            return false;
        }

        #endregion
        

        #region INotifyPropertyChanged Members

        //public event PropertyChangedEventHandler PropertyChanged;

        //public void RaisePropertyChanged(string propertyName)
        //{
        //    if (PropertyChanged != null)
        //        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        //}

        #endregion
    }

    public enum Gamer
    {
        EsGanador = 1,
        EsPerdedor = 2,
        ContinuaJuego = 3
    }
    
}
