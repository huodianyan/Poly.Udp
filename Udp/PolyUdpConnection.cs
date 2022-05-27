using System;
using System.Net;
using System.Threading;

namespace Poly.Udp
{
    public class PolyUdpConnection
    {
        public readonly long connId;
        public readonly EndPoint endPoint;
        private readonly APolyUdpBase udpBase;
        //internal Socket client;

        public int[] SendSeqCounters = new int[4];

        //public PolyUdpPacket[] receivePacketHeads = new PolyUdpPacket[4];
        //public ushort[] ReceiveSeqCounters = new ushort[4];
        public PolyUdpPacketQueue[] PacketQueues = new PolyUdpPacketQueue[4];

        internal bool IsConnected { get; set; }

        public PolyUdpConnection(APolyUdpBase udpBase, long connId, EndPoint endPoint)
        {
            this.udpBase = udpBase;
            //this.client = client;
            this.connId = connId;
            this.endPoint = endPoint;
            for (int i = 0; i < 4; i++)
            {
                PacketQueues[i] = new PolyUdpPacketQueue();
            }
        }
        public override string ToString()
        {
            return $"{connId}[{endPoint}]";
        }
        internal void Disconnect()
        {
            if (!IsConnected) return;
            IsConnected = false;
            udpBase.OnConnectionDisconnect(this);
        }
        internal ushort GetSeqId(EUdpMethod method)
        {
            return (ushort)Interlocked.Increment(ref SendSeqCounters[(byte)method]);
        }
        internal void PollEvents()
        {
            for (int i = 0; i < 4; i++)
            {
                var queue = PacketQueues[i];
                //lock (queue)
                {
                    var packet = queue.GetPacket();
                    while (packet != null)
                    {
                        //Console.WriteLine($"{GetType().Name}.PollEvents: {connId},{queue.Count},{packet}");
                        udpBase.OnConnectionRecieve(this, packet.Segment, (EUdpMethod)i);
                        if (packet.Segment.Array != null)
                            udpBase.arrayPool.Return(packet.Segment.Array);
                        //pool packet
                        packet = queue.GetPacket();
                    }
                }
            }
        }
        internal void OnReceiveData(ArraySegment<byte> segement, EUdpCmd cmd, EUdpMethod method)
        {
            var seqId = (ushort)udpBase.ReadInt16(segement.Array, segement.Offset);
            //var seqCounter = ReceiveSeqCounters[(byte)method];
            var count = segement.Count - 2;
            ArraySegment<byte> seg = default;
            if (count > 0)
            {
                var data = udpBase.arrayPool.Rent(count);
                Buffer.BlockCopy(segement.Array, segement.Offset + 2, data, 0, count);
                seg = new ArraySegment<byte>(data, 0, count);
            }
            var packet = new PolyUdpPacket
            {
                Segment = seg, //new ArraySegment<byte>(segement.Array, segement.Offset + 2, segement.Count - 2),
                SeqId = seqId
            };
            var queue = PacketQueues[(byte)method];
            //lock (queue)
            {
                queue.AddPacket(packet);
                //Console.WriteLine($"{GetType().Name}.OnReceiveData: {connId},{seqId},{cmd},{method},{queue.Count}");
            }
            //udpBase.OnConnectionRecieve(this, packet.Segment, method);
        }
    }
}
