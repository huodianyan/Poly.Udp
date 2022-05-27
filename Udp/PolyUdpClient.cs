using System;
using System.Net;

namespace Poly.Udp
{
    public class PolyUdpClient : APolyUdpBase
    {
        private PolyUdpConnection connection;
        private IPEndPoint endPoint;
        private volatile bool connecting;
        public bool IsConnected => connection != null && connection.IsConnected;

        public PolyUdpClient(IByteArrayPool arrayPool = null) : base(arrayPool)
        {
        }
        public bool Connect(string address, int port)
        {
            if (connecting || IsConnected)
                return IsConnected;
            connecting = true;
            try
            {
                endPoint = new IPEndPoint(IPAddress.Parse(address), port);
                client.Connect(endPoint);
                connecting = false;

                client.SendTimeout = SendTimeout;
                connection = new PolyUdpConnection(this, 0, endPoint);
                //OnConnectionConnect(connection);
                Receive();
                Console.WriteLine($"{GetType().Name}.Connect: {address},{port}");

                //send request
                Send(connection, default, EUdpCmd.ConnectRequest);
            }
            catch (Exception exception)
            {
                Disconnect();
                Console.Error.WriteLine("Client Exception: " + exception);
            }
            return IsConnected;
        }
        public void Disconnect()
        {
            if (IsConnected)
            {
                connection.Disconnect();
                connection = null;
            }
            client.Close();
            connecting = false;
            Console.WriteLine($"{GetType().Name}.Disconnect:");
        }
        protected override void OnReceiveData(EndPoint endPoint, ArraySegment<byte> segement, EUdpCmd cmd, EUdpMethod method)
        {
            if (!connection.IsConnected)
            {
                if (cmd == EUdpCmd.ConnectAccept)
                {
                    connection.IsConnected = true;
                    OnConnectionConnect(connection);
                }
            }
            else
                connection.OnReceiveData(segement, cmd, method);
        }
        public override void PollEvents()
        {
            connection?.PollEvents();
        }
        public override void Send(long connId, ArraySegment<byte> data, EUdpMethod method = EUdpMethod.Unreliable)
        {
            if (connection == null || !connection.IsConnected) return;
            Send(connection, data, EUdpCmd.Data, method);
        }

        //public bool Send(ArraySegment<byte> data, EUdpMethod method = EUdpMethod.Unreliable)
        //{
        //    if (!IsConnected) return false;
        //    if (data.Count > MaxMessageSize)
        //    {
        //        Console.Error.WriteLine($"Client.Send: message too big: {data.Count}. Limit: {MaxMessageSize}");
        //        return false;
        //    }
        //    connection.Send(data, method);
        //    return true;
        //}
    }
}
