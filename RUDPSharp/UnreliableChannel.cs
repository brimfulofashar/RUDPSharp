using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;

namespace RUDPSharp
{
    public class UnreliableChannel {

        ConcurrentQueue<PendingPacket> outgoing;
        ConcurrentQueue<PendingPacket> incoming;
        FragmentAssembler fragmentAssembler = new FragmentAssembler();

        protected ConcurrentQueue<PendingPacket> Outgoing => outgoing;
        protected ConcurrentQueue<PendingPacket> Incoming => incoming;

        protected int MaxBufferSize = 1024;
        private ushort nextFragmentId = 0;

        public UnreliableChannel (int maxBufferSize = 1024)
        {
            outgoing = new ConcurrentQueue<PendingPacket> ();
            incoming = new ConcurrentQueue<PendingPacket> ();
            MaxBufferSize = maxBufferSize;
        }
        public bool TryGetNextOutgoingPacket (out PendingPacket packet)
        {
            packet = null;
            if (outgoing.Count == 0)
                return false;
            return outgoing.TryDequeue (out packet);
        }

        public virtual bool TryGetNextIncomingPacket (out PendingPacket packet)
        {
            packet = null;
            if (incoming.Count == 0)
                return false;
            return incoming.TryDequeue (out packet);
        }
        public virtual PendingPacket QueueOutgoingPacket (EndPoint endPoint, Packet packet)
        {
            // Check if packet needs fragmentation (leaving room for headers)
            // Fragment overhead = header(1) + sequence(2) + fragmentId(2) + fragmentIndex(1) + totalFragments(1) = 7 bytes
            const int NON_FRAGMENT_OVERHEAD = 3; // header(1) + sequence(2)
            const int FRAGMENT_METADATA_SIZE = 4; // fragmentId(2) + fragmentIndex(1) + totalFragments(1)
            int maxPayloadSize = MaxBufferSize - NON_FRAGMENT_OVERHEAD - FRAGMENT_METADATA_SIZE;
            
            if (packet.Payload.Length > maxPayloadSize && !packet.Fragmented)
            {
                // Need to fragment this packet
                ushort fragmentId = nextFragmentId++;
                byte[] payload = packet.Payload.ToArray();
                int totalFragments = (payload.Length + maxPayloadSize - 1) / maxPayloadSize; // ceiling division
                
                if (totalFragments > 255)
                {
                    throw new InvalidOperationException($"Payload too large: {payload.Length} bytes would require {totalFragments} fragments (max 255)");
                }
                
                PendingPacket lastPendingPacket = null;
                for (int i = 0; i < totalFragments; i++)
                {
                    int offset = i * maxPayloadSize;
                    int length = Math.Min(maxPayloadSize, payload.Length - offset);
                    ReadOnlySpan<byte> fragmentPayload = new ReadOnlySpan<byte>(payload, offset, length);
                    
                    var fragmentPacket = new Packet(packet.PacketType, packet.Channel, packet.Sequence, fragmentPayload, fragmented: true);
                    fragmentPacket.SetFragmentInfo(fragmentId, (byte)i, (byte)totalFragments);
                    
                    lastPendingPacket = PendingPacket.FromPacket(endPoint, fragmentPacket, incoming: false);
                    outgoing.Enqueue(lastPendingPacket);
                }
                
                return lastPendingPacket;
            }
            else
            {
                var pendingPacket = PendingPacket.FromPacket (endPoint, packet, incoming: false);
                outgoing.Enqueue (pendingPacket);
                return pendingPacket;
            }
        }

        public virtual PendingPacket QueueIncomingPacket (EndPoint endPoint, Packet packet)
        {
            if (packet.Fragmented) {
                // Add fragment to assembler
                fragmentAssembler.AddFragment(packet);
                
                // Check if we have a complete message
                if (fragmentAssembler.TryGetCompleteMessage(out byte[] data, out PacketType packetType, out Channel channel, out ushort sequence))
                {
                    // Create a non-fragmented packet with the reassembled data
                    var reassembledPacket = new Packet(packetType, channel, sequence, data, fragmented: false);
                    var pendingPacket = PendingPacket.FromPacket(endPoint, reassembledPacket);
                    incoming.Enqueue(pendingPacket);
                    return pendingPacket;
                }
                
                // Fragment stored but message not complete yet
                return null;
            }
            var pending = PendingPacket.FromPacket (endPoint, packet);
            incoming.Enqueue (pending);
            return pending;
        }

        public virtual IEnumerable<PendingPacket> GetPendingOutgoingPackets ()
        {
            while (TryGetNextOutgoingPacket (out PendingPacket pendingPacket)) {
                yield return pendingPacket;
            }
        }

        public virtual IEnumerable<PendingPacket> GetPendingIncomingPackets ()
        {
            // Cleanup old fragments periodically
            fragmentAssembler.CleanupOldFragments();
            
            while (TryGetNextIncomingPacket (out PendingPacket pendingPacket)) {
                yield return pendingPacket;
            }
        }
    }
}
