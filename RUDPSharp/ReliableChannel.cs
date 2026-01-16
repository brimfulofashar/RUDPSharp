using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace RUDPSharp
{
    public class ReliableChannel : InOrderChannel {
        PacketAcknowledgement acknowledgement = new PacketAcknowledgement();
        public ReliableChannel(int maxBufferSize = 1024) : base (maxBufferSize)
        {
        }

        public override PendingPacket QueueOutgoingPacket(EndPoint endPoint, Packet packet)
        {
            var pendingPacket = base.QueueOutgoingPacket (endPoint, packet);
            acknowledgement.HandleOutgoingPackage(packet.Sequence, pendingPacket);
            return pendingPacket;
        }
        public override PendingPacket QueueIncomingPacket (EndPoint endPoint, Packet packet)
        {
            // Don't process ACKs or handle acknowledgment for fragments
            // Fragments will be handled in the base class and acknowledgment will happen
            // after reassembly
            if (packet.Fragmented) {
                return base.QueueIncomingPacket (endPoint, packet);
            }
            
            if (!acknowledgement.HandleIncommingPacket(packet))
            {
                QueueOutgoingPacket(endPoint, new Packet(PacketType.Ack, packet.Channel, packet.Sequence, Array.Empty<byte> ()));
                return base.QueueIncomingPacket (endPoint, packet);
            }
            // ignore Ack Packets
            return null;
        }

        public override IEnumerable<PendingPacket> GetPendingOutgoingPackets ()
        {
            foreach (var p in acknowledgement.GetPacketsToResent())
                yield return p;
            foreach (var p in base.GetPendingOutgoingPackets())
                yield return p;
        }
    }
}