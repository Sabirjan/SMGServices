﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SMG.Socket
{
    public class TcpSocketClient
    {
        #region events

        private event RecvEventHandler onRecv;

        public event RecvEventHandler OnRecv
        {
            add { onRecv += value; }
            remove { onRecv -= value; }
        }

        #endregion

        private string ip;
        private int port;
        private byte[] buffer;
        private List<byte> recvbuffers;
        private bool connected;
        private Socket workSocket;
        private Thread recvThread;
        private ManualResetEvent recvDone;
        private ReaderWriterLockSlim locker = new ReaderWriterLockSlim();

        public string LocalIPAddress { get; private set; }

        public TcpSocketClient(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
            this.recvbuffers = new List<byte>();
            this.buffer = new byte[TransferSet.BufferSize];
            recvDone = new ManualResetEvent(false);
        }

        public TcpSocketClient(Socket socket)
        {
            this.workSocket = socket;
            this.recvbuffers = new List<byte>();
            this.buffer = new byte[TransferSet.BufferSize];
            recvDone = new ManualResetEvent(false);
            connected = true;
        }

        public void Connect()
        {
            try
            {
                if (!connected)
                {
                    workSocket = workSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    workSocket.BeginConnect(new IPEndPoint(IPAddress.Parse(ip), port), (ar) =>
                    {
                        try
                        {
                            workSocket.EndConnect(ar);
                            connected = true;
                        }
                        catch (SocketException e)
                        {
                            SocketExceptionHandler(e);
                        }
                    }, null);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void RecvCallback(IAsyncResult ar)
        {
            try
            {
                try
                {
                    SocketError sokcetError;
                    int len = workSocket.EndReceive(ar, out sokcetError);
                    if (len > 0)
                    {
                        if (buffer[len - 1] == TransferSet.EndByte)
                        {
                            byte[] data = new byte[len - 1];
                            Buffer.BlockCopy(buffer, 0, data, 0, len - 1);
                            recvbuffers.AddRange(data);
                            byte[] allBuffers = recvbuffers.ToArray();

                            buffer = new byte[TransferSet.BufferSize];
                            recvbuffers.Clear();
                            recvDone.Set();

                            if (onRecv != null)
                            {
                                //执行所有接受委托事件
                                foreach (var inv in onRecv.GetInvocationList())
                                {
                                    var _onRecv = (RecvEventHandler)inv;
                                    _onRecv(this, allBuffers);
                                }
                            }
                        }
                        else
                        {
                            byte[] data = new byte[len];
                            Buffer.BlockCopy(buffer, 0, data, 0, len);
                            recvbuffers.AddRange(data);
                            workSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(RecvCallback), null);
                        }
                    }
                    else
                    {
                        throw new SocketException((int)sokcetError);
                    }
                }
                catch (SocketException e)
                {
                    recvDone.Set();
                    SocketExceptionHandler(e);
                }
            }
            catch (SocketException e)
            {
                SocketExceptionHandler(e);
            }
        }

        public void Start()
        {
            if (connected)
            {
                try
                {
                    if (recvThread != null && recvThread.IsAlive)
                    {
                        recvThread.Abort();
                    }

                    recvThread = new Thread(() =>
                    {
                        try
                        {
                            while (connected)
                            {
                                recvDone.Reset();
                                workSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(RecvCallback), null);
                                recvDone.WaitOne();
                            }
                        }
                        catch (SocketException e)
                        {
                            SocketExceptionHandler(e);
                        }
                    });
                    recvThread.IsBackground = true;
                    recvThread.Start();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public void Send(byte[] data)
        {
            try
            {
                if (connected)
                {
                    workSocket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
                    {
                        try
                        {
                            SocketError socketError;
                            int len = workSocket.EndSend(ar, out socketError);
                            if (len < data.Length) throw new SocketException((int)socketError);
                        }
                        catch (SocketException e)
                        {
                            SocketExceptionHandler(e);
                        }
                    }, null);
                }
            }
            catch (SocketException e)
            {
                SocketExceptionHandler(e);
            }
        }

        public void Disconnect()
        {
            locker.EnterWriteLock();

            try
            {
                if (connected)
                {
                    workSocket.BeginDisconnect(false, (ar) =>
                    {
                        try
                        {
                            workSocket.EndDisconnect(ar);
                            workSocket.Shutdown(SocketShutdown.Both);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        finally
                        {
                            connected = false;

                            try
                            {
                                workSocket.Close();
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                            Console.WriteLine("已断开连接");
                        }
                    }, null);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            locker.ExitReadLock();
        }

        private void SocketExceptionHandler(SocketException e)
        {
            if(e.SocketErrorCode == SocketError.ConnectionAborted ||
                e.SocketErrorCode == SocketError.ConnectionReset)
            {
                this.Disconnect();
            }
            else
            {
                Console.WriteLine(e);
            }
        }

    }
}