using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Poly.Udp
{
    public class PolyUdpServer : APolyUdpBase
    {
        protected long connIdCounter;
        protected Dictionary<EndPoint, PolyUdpConnection> connDict;
        protected Dictionary<long, PolyUdpConnection> connIdDict;

        public PolyUdpServer(IByteArrayPool arrayPool = null) : base(arrayPool)
        {
            connDict = new Dictionary<EndPoint, PolyUdpConnection>();
            connIdDict = new Dictionary<long, PolyUdpConnection>();
        }
        public void Start(string address, int port)
        {
            client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
            client.Bind(new IPEndPoint(IPAddress.Parse(address), port));
            Console.WriteLine($"{GetType().Name}.Start: {address},{port}");
            Receive();
        }
        public void Stop()
        {
            client.Close();
            Console.WriteLine($"{GetType().Name}.Stop:");
        }

        public override void PollEvents()
        {
            foreach (var conn in connDict.Values)
            {
                conn.PollEvents();
            }
        }
        protected override void OnReceiveData(EndPoint endPoint, ArraySegment<byte> segement, EUdpCmd cmd, EUdpMethod method)
        {
            if (!connDict.TryGetValue(endPoint, out var conn))
            {
                if (cmd == EUdpCmd.ConnectRequest)
                {
                    var connId = connIdCounter++;
                    conn = new PolyUdpConnection(this, connId, endPoint);
                    connDict.Add(endPoint, conn);
                    connIdDict.Add(connId, conn);
                    conn.IsConnected = true;
                    OnConnectionConnect(conn);

                    Send(conn, segement, EUdpCmd.ConnectAccept);
                }
            }
            else
                conn.OnReceiveData(segement, cmd, method);

        }
        public override void Send(long connId, ArraySegment<byte> data, EUdpMethod method = EUdpMethod.Unreliable)
        {
            if (!connIdDict.TryGetValue(connId, out var conn))
                return;
            if (!conn.IsConnected) return;
            Send(conn, data, EUdpCmd.Data, method);
        }
    }

}
