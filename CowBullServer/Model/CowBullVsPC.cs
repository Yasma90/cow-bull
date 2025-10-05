using System;
using System.Windows;

namespace CowBullServer.Model
{
    public class CowBullVsPC : CowBulls
    {
        //private ServerConnection _serverCONNECTION;
        public ServerSocket Server { get; set; }

        private CowBulls _vacaTors;

        public CowBullVsPC( /*int cantCifraNum*/) : base()
        {
            //GenerarNumeroAleatorio(4);
            Server = new ServerSocket();
            //_server.Send_Msg(GetNumeroEncontrar);
        }

        public void GenerarNumeroAleatorio(int cantCifraNum)
        {
            var numeroAleatorioGenerado = "";
            var aleatorio = new Random();

            while (numeroAleatorioGenerado.Length != cantCifraNum)
            {
                var cifraAleatoria = aleatorio.Next(1, 10);

                //char caracterCifra = Character.forDigit(cifraAleatoria, 10);
                var cifra = char.Parse(cifraAleatoria.ToString());
                if (!InArray(numeroAleatorioGenerado, cifra /*caracterCifra*/))
                {
                    numeroAleatorioGenerado += cifra; //caracterCifra
                }
            }
            NumeroEncontrar = numeroAleatorioGenerado;
            //return numeroEncontrar;
        }

        public void Jugada(string aNumero)
        {
            var othernumber = Server.Message;
            if (EsValidaJugada(aNumero))
                if (ListNumbersJugados.Count < CANTIDAD_INTENTOS_POSIBLES || !FueEncontradoNumero())
                    ComprobarCertezaJugada(othernumber);

                else
                    MessageBox.Show("Number not Valid", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public Gamer ResultadosJuego()
        {
            if (FueEncontradoNumero())
            {
                return Gamer.EsGanador;
            }
            return cantidadJugadasRealizadas == CANTIDAD_INTENTOS_POSIBLES ? Gamer.EsPerdedor : Gamer.ContinuaJuego;
        }
    }
}