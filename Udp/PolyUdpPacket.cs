using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Poly.Udp
{
    public class PolyUdpPacket
    {
        public ArraySegment<byte> Segment;
        public ushort SeqId;
        public PolyUdpPacket Pre;
        public PolyUdpPacket Next;

        public override string ToString()
        {
            var text = Encoding.ASCII.GetString(Segment.Array, Segment.Offset, Segment.Count);
            return $"{Pre}<-{SeqId}[{text}]->{Next}";
        }
    }
    public class PolyUdpPacketQueue
    {
        public PolyUdpPacket Head;
        public PolyUdpPacket Tail;
        public int Count;
        public ushort SeqId;

        public PolyUdpPacketQueue()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void AddPacket(PolyUdpPacket packet)
        {
            lock (this)
            {
                if (Head == null)
                    Head = Tail = packet;
                else
                {
                    Tail.Next = packet;
                    packet.Pre = Tail;
                    Tail = packet;
                }
                Count++;
                //Console.WriteLine($"{GetType().Name}.AddPacket: {SeqId},{Count},{packet}");
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual PolyUdpPacket GetPacket()
        {
            lock (this)
            {
                if (Head == null) return null;
                var packet = Head;
                Head = Head.Next;
                if (Head == null)
                    Tail = null;
                else
                    Head.Pre = null;
                Count--;
                SeqId = packet.SeqId;
                packet.Next = null;
                //Console.WriteLine($"{GetType().Name}.GetPacket: {SeqId},{Count},{packet}");
                return packet;
            }
        }
    }
}
