using System;
using System.Windows;

namespace CowBullClient.Model
{
    public class CowBullVsPC : CowBulls
    {
        private CowBulls _vacaTors;

        //public Connection _connection { get; set; }
        public ClientSocket Client { get; set; }
        public NumeroJugado NumJugados()
        {
            return new NumeroJugado();
        }

        private int count = 0;
        public CowBullVsPC(/*int aCantidadCifrasNumeroEncontrar*/) : base()
        {
            //_connection=new Connection();
            Client = new ClientSocket();
            //GenerarNumeroAleatorio(4);
            //SendNumber(GetNumeroEncontrar);
        }

        private void GenerarNumeroAleatorio(int aCantidadCifrasNumeroEncontrar)
        {
            var numeroAleatorioGenerado = "";
            var aleatorio = new Random();

            while (numeroAleatorioGenerado.Length != aCantidadCifrasNumeroEncontrar)
            {
                var cifraAleatoria = aleatorio.Next(1, 10);

                //char caracterCifra = Character.forDigit(cifraAleatoria, 10);
                var cifra = char.Parse(cifraAleatoria.ToString());
                if (!InArray(numeroAleatorioGenerado, cifra /*caracterCifra*/))
                {
                    numeroAleatorioGenerado += cifra; //caracterCifra
                }
            }
            GetNumeroEncontrar = numeroAleatorioGenerado;
            //return numeroEncontrar;
        }

        void SendNumber(string sendNumber)
        {
            Client.Send_Msg(sendNumber);
            var answerTV = Client.Message;

            if (Client.Message != "")
            {
                answerTV = Client.Message;
            }
            var jugada = answerTV.Split(' ');
            var numJug = new NumeroJugado()
            {
                Numero = jugada[0],
                Toros = int.Parse(jugada[1]),
                Vacas = int.Parse(jugada[2])
            };
            CertezaJugada(numJug);
        }

        public void Jugada(string aNumero)
        {
            if (count == 0)
            {
                GetNumeroEncontrar = Client.Message;
                count++;
            }
            if (EsValidaJugada(aNumero))
            {
                if (ListNumbersJugados.Count < CANTIDAD_INTENTOS_POSIBLES || !FueEncontradoNumero())
                {
                    //GetNumeroEncontrar = _connection.TxtDataRx;
                    ComprobarCertezaJugada(aNumero);
                    //CertezaJugada(aNumero);
                }
            }
            else
            {
                MessageBox.Show("Number not Valid", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public int ResultadosJuego()
        {
            if (FueEncontradoNumero())
            {
                return ES_GANADOR;
            }
            return cantidadJugadasRealizadas == CANTIDAD_INTENTOS_POSIBLES ? ES_PERDEDOR : CONTINUA_JUEGO;
        }
    }
}
