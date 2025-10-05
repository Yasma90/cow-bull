using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CowBullServer.Model
{
    public class ServerConnection
    {
        public string TxtDataRx { get; set; }
        public byte[] m_DataBuffer = new byte[10];
        public IAsyncResult m_asynResult { get; set; }
        public Socket m_socWorker { get; set; }
        public AsyncCallback pfnCallBack;
        public Socket m_socListener;

        public SocketPermission permission { get; set; }
        private System.Timers.Timer tmrEscuchar = new System.Timers.Timer();

        public ServerConnection()
        {
            permission = new SocketPermission(NetworkAccess.Accept,
                TransportType.Tcp, "", SocketPermission.AllPorts);
            StartListening();
            tmrEscuchar.Elapsed+=tmrEscuchar_Elapsed;
        }

        private TcpClient _cliente;
        private void tmrEscuchar_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            var ser = new TcpListener(IPAddress.Any, 1003);
            if (ser.Pending()) // Determina si hay conexiones pendientes
            {
                _cliente = ser.AcceptTcpClient();
            }
        }

        public void StartListening()
        {
            m_socListener = new
                Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var ipLocal = new IPEndPoint(IPAddress.Any, 8221);
            m_socListener.Bind(ipLocal);
            m_socListener.Listen(4);
            m_socListener.BeginAccept(new AsyncCallback(OnClientConnect), null);
            //cmdListen.Enabled = false;
        }

        public void OnClientConnect(IAsyncResult asyn)
        {
            m_socWorker = m_socListener.EndAccept(asyn);
            WaitForData(m_socWorker);
        }

        private void WaitForData(Socket m_socWorker)
        {
            //m_asynResult is declared of type IAsyncResult and assumming that m_socClient has made a connection.
            m_asynResult =
                m_socWorker.BeginReceive(m_DataBuffer, 0, m_DataBuffer.Length, SocketFlags.None, null, null);
            //`TODO:Other way (without one way)
            if (m_asynResult.AsyncWaitHandle.WaitOne())
            {
                var iRx = 0;
                iRx = m_socWorker.EndReceive(m_asynResult);
                var chars = new char[iRx + 1];
                var d = Encoding.UTF8.GetDecoder();
                var charLen = d.GetChars(m_DataBuffer, 0, iRx, chars, 0);
                var szData = new String(chars);
                //var txtDataRx_Text = "";
                TxtDataRx += szData; //este es el que tiene los datos recibidos
            }
        }

        public void Send(String Txx)
        {
            try
            {
                var txx = Encoding.ASCII.GetBytes(Txx);
                m_socListener.Send(txx);
            }
            catch (SocketException se)
            {
                MessageBox.Show(se.Message);
            }
            //var Rx = new byte[1024];
            //var iRx = m_socListener.Receive(Rx);
        }

    }

    public class ServerSocket
    {

        private SocketPermission permission;
        private Socket sListener;
        private IPEndPoint ipEndPoint;
        public Socket handler { get; set; }


        private readonly System.Timers.Timer _tmrEscuchar = new System.Timers.Timer();

        //private TextBox tbAux = new TextBox();

        public ServerSocket()
        {
            //tbAux.SelectionChanged += tbAux_SelectionChanged;

            //Start_Button.IsEnabled = true;
            //StartListen_Button.IsEnabled = false;
            //Send_Button.IsEnabled = false;
            //Close_Button.IsEnabled = false;
            Start_Listen();

            _tmrEscuchar.Elapsed += tmrEscuchar_Elapsed;
        }

        static void tmrEscuchar_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void tbAux_SelectionChanged(object sender, RoutedEventArgs e)
        {
            //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate()
            //{
            //    //tbMsgReceived.Text = tbAux.Text;
            //}
            //);
        }

        public void Start_Listen()
        {
            try
            {
                // Creates one SocketPermission object for access restrictions
                permission = new SocketPermission(
                    NetworkAccess.Accept, // Allowed to accept connections 
                    TransportType.Tcp, // Defines transport types 
                    "", // The IP addresses of local host 
                    SocketPermission.AllPorts // Specifies all ports 
                    );

                // Listening Socket object 
                sListener = null;

                // Ensures the code to have permission to access a Socket 
                permission.Demand();

                // Resolves a host name to an IPHostEntry instance 
                var ipHost = Dns.GetHostEntry("");

                // Gets first IP address associated with a localhost : [0] Iv6 [1] Iv4
                var ipAddr = ipHost.AddressList[1];
                

                // Creates a network endpoint 
                ipEndPoint = new IPEndPoint(ipAddr, 4510);

                // Create one Socket object to listen the incoming connection 
                sListener = new Socket(
                    ipAddr.AddressFamily,
                    SocketType.Stream,
                    ProtocolType.Tcp
                    );

                // Associates a Socket with a local endpoint 
                sListener.Bind(ipEndPoint);

                //tbStatus.Text = "Server started.";
                //Start_Button.IsEnabled = false;
                //StartListen_Button.IsEnabled = true;

                Listen_Click();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        public void Listen_Click()
        {
            try
            {
                //Todo:
                // Places a Socket in a listening state and specifies the maximum 
                // Length of the pending connections queue 
                sListener.Listen(3);

                // Begins an asynchronous operation to accept an attempt 
                AsyncCallback aCallback = AcceptCallback;
                sListener.BeginAccept(aCallback, sListener);

                
                var ipclien=sListener.RemoteEndPoint.AddressFamily;
                var ipClient=ipclien.ToString();
                //tbStatus.Text = "Server is now listening on " + ipEndPoint.Address + " port: " + ipEndPoint.Port;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            // A new Socket to handle remote host communication 
            //Socket handler = null;

            handler = new Socket(new AddressFamily(),
                SocketType.Stream,
                ProtocolType.Tcp
                );
            try
            {
                // Receiving byte array 
                var buffer = new byte[1024];
                // Get Listening Socket object 
                var listener = (Socket) ar.AsyncState;
                // Create a new socket 
                handler = listener.EndAccept(ar);

                // Using the Nagle algorithm 
                handler.NoDelay = false;

                // Creates one object array for passing data 
                var obj = new object[2];
                obj[0] = buffer;
                obj[1] = handler;

                // Begins to asynchronously receive data 
                handler.BeginReceive(
                    buffer, // An array of type Byt for received data 
                    0, // The zero-based position in the buffer  
                    buffer.Length, // The number of bytes to receive 
                    SocketFlags.None, // Specifies send and receive behaviors 
                    new AsyncCallback(ReceiveCallback), //An AsyncCallback delegate 
                    obj // Specifies infomation for receive operation 
                    );

                // Begins an asynchronous operation to accept an attempt 
                var aCallback = new AsyncCallback(AcceptCallback);
                listener.BeginAccept(aCallback, listener);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Fetch a user-defined object that contains information 
                var obj = new object[2];
                obj = (object[]) ar.AsyncState;

                // Received byte array 
                var buffer = (byte[]) obj[0];

                // A Socket to handle remote host communication. 
                handler = (Socket) obj[1];

                // Received message 
                var content = string.Empty;


                // The number of bytes received. 
                var bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    content += Encoding.Unicode.GetString(buffer, 0,
                        bytesRead);

                    // If message contains "<Client Quit>", finish receiving
                    if (content.IndexOf("#", System.StringComparison.Ordinal) > -1)
                    {
                        // Convert byte array to string
                        var str = content.Substring(0, content.LastIndexOf("#", System.StringComparison.Ordinal));

                        //this is used because the UI couldn't be accessed from an external Thread
                        //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate()
                        //{
                        //    tbAux.Text = "Read " + str.Length * 2 + " bytes from client.\n Data: " + str;
                        //}
                        //);
                        Message = str;
                    }
                    else
                    {
                        // Continues to asynchronously receive data
                        var buffernew = new byte[1024];
                        obj[0] = buffernew;
                        obj[1] = handler;
                        handler.BeginReceive(buffernew, 0, buffernew.Length,
                            SocketFlags.None,
                            new AsyncCallback(ReceiveCallback), obj);
                    }

                    //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate()
                    //{
                    //    tbAux.Text = content;
                    //}
                    //);
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        public string Message { get; set; }

        public void Send_Msg(string msg)
        {
            try
            {
                // Convert byte array to string 
                var str = msg;

                // Prepare the reply message 
                var byteData =
                    Encoding.Unicode.GetBytes(str);

                // Sends data asynchronously to a connected Socket 
                handler.BeginSend(byteData, 0, byteData.Length, 0,
                    new AsyncCallback(SendCallback), handler);

                //Send_Button.IsEnabled = false;
                //Close_Button.IsEnabled = true;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // A Socket which has sent the data to remote host 
                var handler = (Socket) ar.AsyncState;

                // The number of bytes sent to the Socket 
                var bytesSend = handler.EndSend(ar);
                //Console.WriteLine(
                //    "Sent {0} bytes to Client", bytesSend);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        private void Close()
        {
            try
            {
                if (sListener.Connected)
                {
                    sListener.Shutdown(SocketShutdown.Receive);
                    sListener.Close();
                }

                //Close_Button.IsEnabled = false;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }
    }


    internal class ServSocket
    {
        private TcpListener tcpListener;
        private Thread listenerThread;

        //Atributos de Intercambio y Asociaciones    
        private int maxClients;
        private int listenPort = 104;
        private bool mustStop;

        public void Start()
        {
            listenerThread = new Thread(ListenerThread) {IsBackground = true};
            listenerThread.Start();
            //OnServerEvent(new LogEventArgs("Server Started: " + aeTitle + " " + listenPort));
        }


        public void Stop()
        {
            //for (int i = 0; i < activeClients.Count; i++)
            //{
            //    //if (activeClients[i].State == Acse.AssociationState.AssociationEstablished)
            //    //{
            //    //    activeClients[i].Abort( Acse.AbortSource.ServiceProvider,Acse.AbortReason.ReasonNotSpecified);
            //    //}
            //    activeClients[i].Dispose();
            //}
            mustStop = true;
            listenerThread.Join();
            //OnServerEvent(new LogEventArgs("Server Stopped: " + aeTitle + " " + listenPort));
        }



        private Mensaje_Data mdata;
        private StreamReader sread;
        private TcpClient client;
        private void ListenerThread()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, listenPort);
                tcpListener.Start();
                while (!mustStop)
                {
                    if (tcpListener.Pending())
                    {
                        client = tcpListener.AcceptTcpClient();
                        client.ReceiveBufferSize = (128*1024);
                        client.SendBufferSize = (64*1024);
                        var clientIP = client.Client.RemoteEndPoint.ToString().Split(':')[0];
                        //Configurar_Conexion(out acse, clientIP);
                        // ...

                        sread = new StreamReader(client.GetStream());
                        // Cremos un Objeto con lo recibido del cliente
                        Object aux = sread.BaseStream;//ReadToEnd(); // leemos objeto
                        // si el objeto es una instancia de Mensaje_data

                        if (aux is Mensaje_Data)
                        {
                            // casteamos el objeto
                            mdata = (Mensaje_Data) aux;

                            // Analizamos el mensaje recibido
                            // si no es el mensaje FINAL
                            if (!mdata.last_msg)
                            {

                                // Es un mensaje de Accion
                                if (mdata.Action != -1)
                                {
                                    // exec accion
                                    //Exec(mdata.Action);          Exec
                                    //System.out.
                                    //println("[" + TimeStamp + "] "
                                    //        + "Ejecutar Accion " + mdata.Action
                                    //        + " [" + IP_client + "]");
                                }
                            }
                            else
                            {
                                // cerramos socket
                                client.Client.Close();
                                sread.Close();
                                // println("["
                                //         + TimeStamp
                                //         + "] Last_msg detected Conexion cerrada, gracias vuelva pronto");
                                break;
                            }
                        }
                            // Si no es del tipo esperado, se marca error
                            //System.err.println("Mensaje no esperado ");
                        
                    }
                    else
                        Thread.Sleep(100);
                }
                tcpListener.Stop();
            }
            catch (Exception exception)
            {
                //GestorLog.Logger.Error("project: Alas_Server  class:ServerAE method:ListenerThread", exception);
                //OnServerEvent(new LogEventArgs("Exception Reached: " + exception.Message));
            }

        }

        private Socket miCliente;
        public bool Connect()
        {
            //Obtengo datos ingresados en campos
            var IP = "";//ipinput.getText().toString();
            var PORT = 14;//Integer.valueOf(portinput.getText().toString());
            
            try
            {//creamos sockets con los valores anteriores
                
                miCliente = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                Byte ipBytes = Byte.Parse(IP);

                var endPIP = new IPEndPoint(new IPAddress(ipBytes), listenPort);

                miCliente.Connect(endPIP);
                miCliente.Listen(1);
                //miCliente.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);

                //si nos conectamos
                return miCliente.Connected;
            }
            catch (Exception e)
            {
                //Si hubo algun error mostrmos error
                //txtstatus.setTextColor(Color.RED);
                //txtstatus.setText(" !!! ERROR  !!!");
                //Log.e("Error connect()", "" + e);
                return false;
            }
        }

        //Metodo de desconexion
        public void Disconnect()
        {
            try
            {
                //Prepramos mensaje de desconexion
                var msgact = new Mensaje_Data {texto = "", Action = -1, last_msg = true};
                //avisamos al server que cierre el canal
                var val_acc = Snd_Msg(msgact);

                if (!val_acc)
                {//hubo un error
                    //Set_txtstatus(" Error  ", 0);
                    //Change_leds(false);
                    //Log.e("Disconnect() -> ", "!ERROR!");

                }
                else
                {//ok nos desconectamos
                    //Set_txtstatus("Desconectado", 0);
                    //camibmos led a rojo
                    //Change_leds(false);
                    //Log.e("Disconnect() -> ", "!ok!");
                    //cerramos socket
                    miCliente.Close();
                }
            }
            catch (IOException e)
            {
                // TODO Auto-generated catch block
                //e.printStackTrace();
            }

            if (!miCliente.Connected) { }
                //Change_leds(false);
        }

        //Enviamos mensaje de accion segun el boton q presionamos
        public void Snd_Action(int bt)
        {
            var msgact = new Mensaje_Data {texto = "", Action = bt, last_msg = false};
            //no hay texto
            //seteo en el valor action el numero de accion
            //no es el ultimo msg
            //mando msg
            var val_acc = Snd_Msg(msgact);
            //error al enviar
            if (!val_acc)
            {
                //Set_txtstatus(" Error  ", 0);
                //Change_leds(false);
                //Log.e("Snd_Action() -> ", "!ERROR!");

            }

            if (!miCliente.Connected) { }
                //Change_leds(false);
        }

        //Envio mensaje de texto
        public void Snd_txt_Msg(String txt)
        {

            var mensaje = new Mensaje_Data();
            //seteo en texto el parametro  recibido por txt
            mensaje.texto = txt;
            //action -1 no es mensaje de accion
            mensaje.Action = -1;
            //no es el ultimo msg
            mensaje.last_msg = false;
            //mando msg
            var val_acc = Snd_Msg(mensaje);
            //error al enviar
            if (!val_acc)
            {
                //Set_txtstatus(" Error  ", 0);
                //Change_leds(false);
                //Log.e("Snd_txt_Msg() -> ", "!ERROR!");
            }
            if (!miCliente.Connected) { }
                //Change_leds(false);
        }


        /*Metodo para enviar mensaje por socket
         *recibe como parmetro un objeto Mensaje_data
         *retorna boolean segun si se pudo establecer o no la conexion
         */
        private StreamWriter sWrite;
        
        public bool Snd_Msg(Mensaje_Data msg)
        {

            try
            {
                //Accedo a flujo de salida
                sWrite = new StreamWriter(client.GetStream());
                //creo objeto mensaje
                Mensaje_Data mensaje;

                if (miCliente.Connected)// si la conexion continua
                {
                    //lo asocio al mensaje recibido
                    mensaje = msg;
                    //Envio mensaje por flujo
                    sWrite.Write(mensaje);
                    //envio ok
                    return true;
                }
                //en caso de que no halla conexion al enviar el msg
                //Set_txtstatus("Error...", 0);//error
                return false;

            }
            catch (IOException e)
            {// hubo algun error
                //Log.e("Snd_Msg() ERROR -> ", "" + e);

                return false;
            }
        }

   
    }
}
