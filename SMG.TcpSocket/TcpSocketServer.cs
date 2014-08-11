﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SMG.TcpSocket
{
    public class TcpSocketServer
    {
        #region events

        private event SendEventHandler onSend;
        private event RecvEventHandler onRecv;

        public event SendEventHandler OnSend
        {
            add { onSend += value; }
            remove { onSend -= value; }
        }

        public event RecvEventHandler OnRecv
        {
            add { onRecv += value; }
            remove { onRecv -= value; }
        }

        #endregion

        private string ip;
        private int port;
        private bool listened;
        private int poolSize;
        private Socket workSocket;
        private Thread acceptThread;
        private ManualResetEvent accpetDone;

        public string BindIPAddress
        {
            get
            {
                if (workSocket != null)
                {
                    return workSocket.LocalEndPoint.ToString();
                }

                return "nil"; 
            }
        }

        public TcpSocketServer(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
            this.accpetDone = new ManualResetEvent(false);
            
        }

        public void Listen(int poolSize)
        {
            try
            {
                if (!listened)
                {
                    workSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    workSocket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
                    workSocket.Listen(poolSize);
                    listened = true;
                    this.poolSize = poolSize;

                    if (acceptThread != null && acceptThread.IsAlive)
                    {
                        acceptThread.Abort();
                    }

                    acceptThread = new Thread(() =>
                    {
                        while (listened)
                        {
                            accpetDone.Reset();

                            workSocket.BeginAccept((ar) =>
                            {
                                try
                                {
                                    var client = workSocket.EndAccept(ar);
                                    var tcpClient = new TcpSocketClient(client);
                                    tcpClient.OnRecv += onRecv;
                                    tcpClient.OnSend += onSend;
                                    tcpClient.Start();

                                    Console.WriteLine(client.RemoteEndPoint.ToString() + " 已连接");
                                }
                                catch (SocketException e)
                                {
                                    Console.WriteLine(e);
                                }
                                finally
                                {
                                    accpetDone.Set();
                                }
                            }, null);

                            accpetDone.WaitOne();
                        }
                    });
                    acceptThread.IsBackground = true;
                    acceptThread.Start();
                }
            }
            catch (Exception e)
            {
                listened = false;
                Console.WriteLine(e);
            }
        }

    }
}
