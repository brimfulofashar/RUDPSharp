using System;
using System.Collections.Generic;
using System.Linq;

namespace RUDPSharp
{
    internal class FragmentAssembler
    {
        private class FragmentCollection
        {
            public byte[] [] Fragments { get; set; }
            public byte TotalFragments { get; set; }
            public int ReceivedCount { get; set; }
            public long LastUpdate { get; set; }
            public PacketType PacketType { get; set; }
            public Channel Channel { get; set; }
            public ushort Sequence { get; set; }
        }

        private Dictionary<ushort, FragmentCollection> fragmentBuffers = new Dictionary<ushort, FragmentCollection>();
        private const long FRAGMENT_TIMEOUT_TICKS = 50000000; // 5 seconds

        public void AddFragment(Packet packet)
        {
            if (!packet.Fragmented)
            {
                throw new ArgumentException("Packet is not fragmented", nameof(packet));
            }

            ushort fragmentId = packet.FragmentId;
            
            if (!fragmentBuffers.ContainsKey(fragmentId))
            {
                fragmentBuffers[fragmentId] = new FragmentCollection
                {
                    Fragments = new byte[packet.TotalFragments][],
                    TotalFragments = packet.TotalFragments,
                    ReceivedCount = 0,
                    LastUpdate = DateTime.Now.Ticks,
                    PacketType = packet.PacketType,
                    Channel = packet.Channel,
                    Sequence = packet.Sequence
                };
            }

            var collection = fragmentBuffers[fragmentId];
            
            if (collection.Fragments[packet.FragmentIndex] == null)
            {
                collection.Fragments[packet.FragmentIndex] = packet.Payload.ToArray();
                collection.ReceivedCount++;
                collection.LastUpdate = DateTime.Now.Ticks;
            }
        }

        public bool TryGetCompleteMessage(out byte[] data, out PacketType packetType, out Channel channel, out ushort sequence)
        {
            data = null;
            packetType = PacketType.Data;
            channel = Channel.None;
            sequence = 0;

            // Find a complete fragment collection
            foreach (var kvp in fragmentBuffers)
            {
                var collection = kvp.Value;
                if (collection.ReceivedCount == collection.TotalFragments)
                {
                    // All fragments received, reassemble
                    int totalLength = collection.Fragments.Sum(f => f.Length);
                    data = new byte[totalLength];
                    int offset = 0;
                    
                    for (int i = 0; i < collection.TotalFragments; i++)
                    {
                        Buffer.BlockCopy(collection.Fragments[i], 0, data, offset, collection.Fragments[i].Length);
                        offset += collection.Fragments[i].Length;
                    }
                    
                    packetType = collection.PacketType;
                    channel = collection.Channel;
                    sequence = collection.Sequence;
                    
                    fragmentBuffers.Remove(kvp.Key);
                    return true;
                }
            }

            return false;
        }

        public void CleanupOldFragments()
        {
            long now = DateTime.Now.Ticks;
            var toRemove = new List<ushort>();

            foreach (var kvp in fragmentBuffers)
            {
                if (now - kvp.Value.LastUpdate > FRAGMENT_TIMEOUT_TICKS)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                fragmentBuffers.Remove(key);
            }
        }
    }
}
