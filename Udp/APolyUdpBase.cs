using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Poly.Udp
{
    public enum EUdpCmd : byte
    {
        Data,
        ConnectRequest,
        ConnectAccept,
        Disconnect,
        Ping,
        Pong,
    }
    public enum EUdpMethod : byte
    {
        Unreliable = 0,
        Reliable = 1,
        Ordered = 2,
        ReliableOrdered = 3,
    }
    public abstract class APolyUdpBase
    {
        internal IByteArrayPool arrayPool;
        //internal UdpClient client;
        protected Socket client;

        public bool NoDelay = true;
        public int MaxMessageSize = 64 * 1024;
        public int SendTimeout = 5000;

        private EndPoint epFrom = new IPEndPoint(IPAddress.Any, 0);
        private AsyncCallback recv = null;
        private byte[] receiveBuffer;
        private int recievePos;
        private int recievePacketSize;
        private byte receivePacketFlag;

        private ConcurrentQueue<SendData> sendQueue;
        private ManualResetEvent sendPending = new ManualResetEvent(false);
        private readonly Thread sendThread;
        //private readonly byte[] sendHeader;
        private readonly byte[] sendBuffer;

        public IByteArrayPool ArrayPool => arrayPool;

        public event Action<long> OnConnectEvent;
        public event Action<long> OnDisconnectEvent;
        public event Action<long, ArraySegment<byte>, EUdpMethod> OnReceiveEvent;

        public APolyUdpBase(IByteArrayPool arrayPool = null)
        {
            this.arrayPool = arrayPool ?? new ByteArrayPool();
            //Client = new UdpClient();
            client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            receiveBuffer = new byte[MaxMessageSize];

            //sendHeader = new byte[4];
            sendBuffer = new byte[MaxMessageSize];
            sendQueue = new ConcurrentQueue<SendData>();
            sendThread = new Thread(() => SendLoop());
            sendThread.IsBackground = true;
            sendThread.Start();
        }
        internal virtual void OnConnectionConnect(PolyUdpConnection connection)
        {
            OnConnectEvent?.Invoke(connection.connId);
        }
        internal virtual void OnConnectionRecieve(PolyUdpConnection connection, ArraySegment<byte> segment, EUdpMethod method)
        {
            //var text = Encoding.ASCII.GetString(segment.Array, segment.Offset, segment.Count);
            //Console.WriteLine($"{GetType().Name}.OnConnectionRecieve: {connection.connId},{text},[{segment.Array.Length},{segment.Offset},{segment.Count}]");
            OnReceiveEvent?.Invoke(connection.connId, segment, method);
            //text = Encoding.ASCII.GetString(segment.Array, segment.Offset, segment.Count);
            //Console.WriteLine($"{GetType().Name}.OnConnectionRecieve1: {connection.connId},{text},[{segment.Array.Length},{segment.Offset},{segment.Count}]");
        }
        internal virtual void OnConnectionError(PolyUdpConnection connection, string error)
        {

        }
        internal virtual void OnConnectionDisconnect(PolyUdpConnection connection)
        {
            OnDisconnectEvent?.Invoke(connection.connId);
        }
        protected void Receive()
        {
            client.BeginReceiveFrom(receiveBuffer, recievePos, MaxMessageSize, SocketFlags.None, ref epFrom, recv = (ar) =>
            {
                try
                {
                    //State so = (State)ar.AsyncState;
                    int bytes = client.EndReceiveFrom(ar, ref epFrom);
                    recievePos += bytes;
                    //Console.WriteLine($"{GetType().Name}.Receive: {epFrom},{bytes},{recievePos}");
                    if (recievePacketSize == 0)
                    {
                        if (recievePos >= 4)
                        {
                            //recievePacketSize = (receiveBuffer[0] << 24) | (receiveBuffer[1] << 16) | (receiveBuffer[2] << 8) | receiveBuffer[3];
                            recievePacketSize = (receiveBuffer[0] << 16) | (receiveBuffer[1] << 8) | (receiveBuffer[2]);
                            receivePacketFlag = receiveBuffer[3];
                        }
                    }
                    if (recievePacketSize > 0)
                    {
                        if (recievePos >= recievePacketSize)
                        {
                            var cmd = (EUdpCmd)(receivePacketFlag & 0x3F);
                            var method = (EUdpMethod)(receivePacketFlag >> 6);

                            //Console.WriteLine($"{GetType().Name}.Receive: {epFrom},{cmd},{method},{recievePacketSize}");
                            OnReceiveData(epFrom, new ArraySegment<byte>(receiveBuffer, 4, recievePacketSize - 4), cmd, method);
                            //RecieveEvent?.Invoke(conn, new ArraySegment<byte>(receiveBuffer, 4, recievePacketSize - 4));
                            recievePos -= recievePacketSize;
                            if (recievePos > 0)
                                Buffer.BlockCopy(receiveBuffer, recievePacketSize, receiveBuffer, 0, recievePos);
                            recievePacketSize = 0;
                        }
                    }
                    client.BeginReceiveFrom(receiveBuffer, recievePos, MaxMessageSize, SocketFlags.None, ref epFrom, recv, null);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Receive: {ex}");
                }
            }, null);
        }
        protected abstract void OnReceiveData(EndPoint endPoint, ArraySegment<byte> segement, EUdpCmd cmd, EUdpMethod method);

        protected struct SendData
        {
            public EndPoint endPoint;
            public ArraySegment<byte> Segment;
            public EUdpCmd cmd;
            public EUdpMethod method;
        }
        #region send
        public void Send(long connId, string text)
        {
            var count = Encoding.ASCII.GetByteCount(text);
            var data = arrayPool.Rent(count);
            Encoding.ASCII.GetBytes(text, 0, text.Length, data, 0);
            //Console.WriteLine($"{GetType().Name}.Send: {connId},{text}");
            Send(connId, new ArraySegment<byte>(data, 0, count));
        }

        public abstract void Send(long connId, ArraySegment<byte> data, EUdpMethod method = EUdpMethod.Unreliable);
        internal void Send(PolyUdpConnection conn, ArraySegment<byte> data, EUdpCmd cmd = EUdpCmd.Data, EUdpMethod method = EUdpMethod.Unreliable)
        {
            var seqId = conn.GetSeqId(method);
            var count = data.Count;
            var dest = arrayPool.Rent(count + 2);
            //Console.Error.WriteLine($"++Send: {count},{dest.Length}");
            if (count > 0)
            {
                Array.Copy(data.Array, data.Offset, dest, 2, count);
                arrayPool.Return(data.Array);
            }
            Write(dest, 0, (short)seqId);
            sendQueue.Enqueue(new SendData
            {
                Segment = new ArraySegment<byte>(dest, 0, count + 2),
                endPoint = conn.endPoint,
                cmd = cmd,
                method = method
            });
            //Console.WriteLine($"{GetType().Name}.Send: {cmd},{seqId},{count}");
            sendPending.Set();
        }
        private void SendLoop()
        {
            try
            {
                //Console.WriteLine($"{GetType().Name}.SendLoop: {client.Connected}");

                //NetworkStream stream = client.GetStream();
                while (true)//client.Connected
                {
                    sendPending.Reset();
                    while (sendQueue.TryDequeue(out var sendData))
                    {
                        var segment = sendData.Segment;
                        var count = segment.Count + 4;
                        //Console.WriteLine($"{GetType().Name}.SendLoop: {sendData.cmd},{count}");
                        sendBuffer[0] = (byte)(count >> 16);
                        sendBuffer[1] = (byte)(count >> 8);
                        sendBuffer[2] = (byte)count;
                        sendBuffer[3] = (byte)(((byte)sendData.cmd) | ((byte)sendData.method) << 6);
                        Buffer.BlockCopy(segment.Array, segment.Offset, sendBuffer, 4, segment.Count);
                        client.SendTo(sendBuffer, count, SocketFlags.None, sendData.endPoint);
                    }
                    sendPending.WaitOne();
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Connection send thread exception: " + exception);
            }
            finally
            {
                //Disconnect();
            }
        }
        #endregion

        #region Read/Write
        internal void Write(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }
        internal void Write(byte[] data, int offset, short value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)(value);
        }
        internal int ReadInt32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }
        internal short ReadInt16(byte[] data, int offset)
        {
            return (short)((data[offset] << 8) | data[offset + 1]);
        }
        #endregion

        public abstract void PollEvents();
    }
}
