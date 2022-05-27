//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;

//namespace Poly.Udp
//{
//    public class UdpConnection
//    {
//        public long connId;
//        public EndPoint endPoint;
//        public UdpPacket packetHead;
//        public ushort SendSequenceId;
//        public ushort ReceiveSequenceId;

//        public UdpConnection(long connId, EndPoint endPoint)
//        {
//            this.connId = connId;
//            this.endPoint = endPoint;
//        }
//        public override string ToString()
//        {
//            return $"{connId}[{endPoint}]";
//        }
//    }
//    public class UdpPacket
//    {
//        public ArraySegment<byte> segment;
//        public ushort SequenceId;
//        public UdpPacket Pre;
//        public UdpPacket Next;
//    }

//    public class UDPSocket
//    {
//        private Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
//        private const int bufSize = 8 * 1024;
//        //private State state = new State();
//        private EndPoint epFrom = new IPEndPoint(IPAddress.Any, 0);
//        private long connIdCounter;
//        private Dictionary<EndPoint, UdpConnection> connDict = new Dictionary<EndPoint, UdpConnection>();

//        private bool isServer;
//        private EndPoint remoteEndPoint;

//        private IByteArrayPool arrayPool;
//        private AsyncCallback recv = null;
//        private byte[] receiveBuffer = new byte[bufSize];
//        private int recievePos;
//        private int recievePacketSize;

//        public event Action<UdpConnection> ConnectEvent;
//        public event Action<UdpConnection> DisconnectEvent;
//        public event Action<UdpConnection, ArraySegment<byte>> ReceiveEvent;

//        //public class State
//        //{
//        //    public byte[] buffer = new byte[bufSize];
//        //    public int readPos;
//        //}

//        public UDPSocket(IByteArrayPool arrayPool = null)
//        {
//            this.arrayPool = arrayPool ?? new ByteArrayPool();
//        }

//        public void Server(string address, int port)
//        {
//            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
//            socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
//            isServer = true;
//            Receive();
//        }

//        public void Client(string address, int port)
//        {
//            remoteEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
//            socket.Connect(remoteEndPoint);
//            Receive();

//            //send connect request
//            SendTo(remoteEndPoint, "ConnectRequest");
//        }

//        public void SendTo(EndPoint endPoint, string text)
//        {
//            var count = Encoding.ASCII.GetByteCount(text) + 4;
//            var data = arrayPool.Rent(count);
//            Encoding.ASCII.GetBytes(text, 0, text.Length, data, 4);
//            data[0] = (byte)(count >> 24);
//            data[1] = (byte)(count >> 16);
//            data[2] = (byte)(count >> 8);
//            data[3] = (byte)count;
//            SendTo(endPoint, new ArraySegment<byte>(data, 0, count));
//        }
//        public void Send(string text)
//        {
//            if (remoteEndPoint == null) return;
//            SendTo(remoteEndPoint, text);
//        }
//        private void SendTo(EndPoint endPoint, ArraySegment<byte> segment, EUdpCmd cmd = EUdpCmd.Data, bool isReliable = false, bool isOrdered = false)
//        {
//            socket.BeginSendTo(segment.Array, segment.Offset, segment.Count, SocketFlags.None, endPoint, (ar) =>
//            {
//                //State so = (State)ar.AsyncState;
//                int bytes = socket.EndSendTo(ar);
//                if (bytes != segment.Count)
//                    Console.Error.WriteLine($"SendTo: {bytes}!={segment.Count}");
//                arrayPool.Return(segment.Array);
//            }, null);
//        }
//        private void Receive()
//        {
//            socket.BeginReceiveFrom(receiveBuffer, recievePos, bufSize, SocketFlags.None, ref epFrom, recv = (ar) =>
//            {
//                //State so = (State)ar.AsyncState;
//                int bytes = socket.EndReceiveFrom(ar, ref epFrom);
//                recievePos += bytes;
//                if (recievePacketSize == 0)
//                {
//                    if (recievePos >= 4)
//                        recievePacketSize = (receiveBuffer[0] << 24) | (receiveBuffer[1] << 16) | (receiveBuffer[2] << 8) | receiveBuffer[3];
//                }
//                if (recievePacketSize > 0)
//                {
//                    if (recievePos >= recievePacketSize)
//                    {
//                        OnReceiveData(epFrom, new ArraySegment<byte>(receiveBuffer, 4, recievePacketSize - 4));
//                        //RecieveEvent?.Invoke(conn, new ArraySegment<byte>(receiveBuffer, 4, recievePacketSize - 4));
//                        recievePos -= recievePacketSize;
//                        if (recievePos > 0)
//                            Buffer.BlockCopy(receiveBuffer, recievePacketSize, receiveBuffer, 0, recievePos);
//                        recievePacketSize = 0;
//                    }
//                }
//                socket.BeginReceiveFrom(receiveBuffer, recievePos, bufSize, SocketFlags.None, ref epFrom, recv, null);
//            }, null);
//        }
//        private void OnReceiveData(EndPoint endPoint, ArraySegment<byte> segement)
//        {
//            //var text = Encoding.ASCII.GetString(segement.Array, segement.Offset, segement.Count);
//            //Console.WriteLine($"OnReceiveData: [{isServer}]{endPoint}: {text}");
//            var flag = segement.Array[0];
//            var command = (EUdpCmd)(flag & 0xC0);
//            var channel = (byte)(flag >> 6);
//            var isReliable = (flag & 0x80) != 0;
//            var isOrdered = (flag & 0x40) != 0;
//            if (!connDict.TryGetValue(endPoint, out var conn))
//            {
//                if (isServer)
//                {
//                    if (command == EUdpCmd.ConnectRequest)
//                    {
//                        var connId = connIdCounter++;
//                        conn = new UdpConnection(connId, epFrom);
//                        connDict.Add(endPoint, conn);

//                        SendTo(endPoint, $"{connId}");
//                        ConnectEvent?.Invoke(conn);
//                    }
//                }
//                else
//                {
//                    if (command == EUdpCmd.ConnectAccept)
//                    {
//                        var connId = long.Parse(text.Substring("ConnectAccept".Length));
//                        conn = new UdpConnection(connId, endPoint);
//                        connDict.Add(endPoint, conn);

//                        //SendTo(epFrom, $"ConnectAccept{connId}");
//                        ConnectEvent?.Invoke(conn);
//                    }
//                }
//            }
//            else
//            {
//                RecieveEvent?.Invoke(conn, text);
//            }

//        }

//        #region Read/Write
//        private void Write(byte[] data, int offset, int value)
//        {
//            data[offset] = (byte)(value >> 24);
//            data[offset + 1] = (byte)(value >> 16);
//            data[offset + 2] = (byte)(value >> 8);
//            data[offset + 3] = (byte)value;
//        }
//        private void Write(byte[] data, int offset, short value)
//        {
//            data[offset] = (byte)(value >> 8);
//            data[offset + 1] = (byte)(value);
//        }
//        private int ReadInt32(byte[] data, int offset)
//        {
//            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
//        }
//        private short ReadInt16(byte[] data, int offset)
//        {
//            return (short)((data[offset] << 8) | data[offset + 1]);
//        }
//        #endregion
//    }
//}