using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;

namespace CowBullClient.Model
{
    public class Connection
    {
        public string TxtDataRx { get; set; }
        private readonly byte[] m_DataBuffer = new byte[10];
        private IAsyncResult m_asynResult;
        public AsyncCallback pfnCallBack;
        public Socket m_socClient;

        public Connection()
        {
            OnConnect();
        }
        #region Reception Asynchronously
        
        // create the socket...
        public void OnConnect()
        {
            m_socClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // get the remote IP address...
            var ip = IPAddress.Parse("127.0.0.1");
            const int iPortNo = 4510; //8221;

            //create the end point
            var ipEnd = new IPEndPoint(ip, iPortNo); //ip.Address
            //connect to the remote host...
            m_socClient.Connect(ipEnd);
            //watch for data ( asynchronously )...
            WaitForData();
        }

        public void WaitForData()
        {
            //TODO:On way
            //if (pfnCallBack == null)
            //    pfnCallBack = new AsyncCallback(OnDataReceived);

            //now start to listen for any data...
            //m_asynResult =
            //    m_socClient.BeginReceive
            //        (m_DataBuffer, 0, m_DataBuffer.Length, SocketFlags.None, pfnCallBack, null);

            //TODO:Other way (without one way)
            //m_asynResult is declared of type IAsyncResult and assumming that m_socClient has made a connection.
            m_asynResult =
                m_socClient.BeginReceive(m_DataBuffer, 0, m_DataBuffer.Length, SocketFlags.None, null, null);

            if (m_asynResult.AsyncWaitHandle.WaitOne())
            {
                var iRx = 0;
                iRx = m_socClient.EndReceive(m_asynResult);
                var chars = new char[iRx + 1];
                var d = Encoding.UTF8.GetDecoder();
                var charLen = d.GetChars(m_DataBuffer, 0, iRx, chars, 0);
                var szData = new String(chars);
                TxtDataRx = "";
                TxtDataRx += szData; //este es el que tiene los datos recibidos
            }
        }

        public void OnDataReceived(IAsyncResult asyn)
        {
            //end receive...
            var iRx = 0;
            iRx = m_socClient.EndReceive(asyn);
            var chars = new char[iRx + 1];
            var d = Encoding.UTF8.GetDecoder();
            var charLen = d.GetChars(m_DataBuffer, 0, iRx, chars, 0);
            var szData = new String(chars);
            WaitForData();
        }

        #endregion

        public void Send(String Txx)
        {
            try
            {
                var txx = Encoding.ASCII.GetBytes(Txx);
                m_socClient.Send(txx);
            }
            catch (SocketException se)
            {
                MessageBox.Show(se.Message);
            }
            var Rx = new byte[1024];
            var iRx = m_socClient.Receive(Rx);
        }
    }

    public class ClientSocket
    {
        // Receiving byte array  
        byte[] bytes = new byte[1024];
        Socket senderSock;

        public ClientSocket()
        {
            //Connect_Server();
        }

        public void Connect_Server()
        {
            try
            {
                // Create one SocketPermission for socket access restrictions 
                var permission = new SocketPermission(
                    NetworkAccess.Connect, // Connection permission 
                    TransportType.Tcp, // Defines transport types 
                    "", // Gets the IP addresses 
                    SocketPermission.AllPorts // All ports 
                    );

                // Ensures the code to have permission to access a Socket 
                permission.Demand();

                // Resolves a host name to an IPHostEntry instance            
                var ipHost = Dns.GetHostEntry("");

                // Gets first IP address associated with a localhost 
                var ipAddr = ipHost.AddressList[1];

                // Creates a network endpoint 
                var ipEndPoint = new IPEndPoint(ipAddr, 4510);

                // Create one Socket object to setup Tcp connection 
                senderSock = new Socket(
                    ipAddr.AddressFamily,// Specifies the addressing scheme 
                    SocketType.Stream,   // The type of socket  
                    ProtocolType.Tcp     // Specifies the protocols  
                    )
                {
                    NoDelay = false   // Using the Nagle algorithm 
                };

                // Establishes a connection to a remote host 
                senderSock.Connect(ipEndPoint);
                //tbStatus.Text = "Socket connected to " + senderSock.RemoteEndPoint.ToString();

            }
            catch (Exception exc) { MessageBox.Show(exc.ToString()); }

        }

        public void Send_Msg(string mssg)
        {
            try
            {
                // Sending message 
                //<#> is the sign for end of data 
                var theMessageToSend = mssg +"#";
                var msg = Encoding.Unicode.GetBytes(theMessageToSend);

                // Sends data to a connected Socket. 
                var bytesSend = senderSock.Send(msg);

                ReceiveDataFromServer();

            }
            catch (Exception exc) { MessageBox.Show(exc.ToString()); }
        }
        
        public string Message { get; set; }

        private void ReceiveDataFromServer()
        {
            try
            {
                // Receives data from a bound Socket. 
                var bytesRec = senderSock.Receive(bytes);

                // Converts byte array to string 
                var theMessageToReceive = Encoding.Unicode.GetString(bytes, 0, bytesRec);

                // Continues to read the data till data isn't available 
                while (senderSock.Available > 0)
                {
                    bytesRec = senderSock.Receive(bytes);
                    theMessageToReceive += Encoding.Unicode.GetString(bytes, 0, bytesRec);
                }
                Message = theMessageToReceive;
                //tbReceivedMsg.Text = "The server reply: " + theMessageToReceive;
            }
            catch (Exception exc) { MessageBox.Show(exc.ToString()); }
        }

        private void Disconnect()
        {
            try
            {
                // Disables sends and receives on a Socket. 
                senderSock.Shutdown(SocketShutdown.Both);

                //Closes the Socket connection and releases all resources 
                senderSock.Close();
            }
            catch (Exception exc) { MessageBox.Show(exc.ToString()); }
        }

    }

}


/* EXAMPLE to SEnd::::::
 * try
{
 String Txx = "Hello There";
 byte[] Txx = System.Text.Encoding.ASCII.GetBytes(Txx);
 move_cam.Send(Txx);
}
catch (SocketException se)
{
 MessageBox.Show ( se.Message );
}
byte [] Rx = new byte[1024];
int iRx = move_cam.Receive(Rx);
 * 
 */
