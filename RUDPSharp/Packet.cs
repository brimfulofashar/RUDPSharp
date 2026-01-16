using System;

namespace RUDPSharp
{
    /*
    * Packet Structure
    *
    * byte header   PacketType|Channel|FragmentedBit
    * byte sequence (low)
    * byte sequence (high)
    * -- If fragmented:
    * byte fragmentId (low)
    * byte fragmentId (high)
    * byte fragmentIndex
    * byte totalFragments
    * -- End if
    * byte+ payload
    */
    public ref struct Packet {
        const int  HEADER_OFFSET = 0;
        const int  SEQUENCE_OFFSET = 1;
        const int  PAYLOAD_OFFSET = 3;
        const int  FRAGMENT_ID_OFFSET = 3;
        const int  FRAGMENT_INDEX_OFFSET = 5;
        const int  FRAGMENT_TOTAL_OFFSET = 6;
        const int  FRAGMENT_PAYLOAD_OFFSET = 7;
        byte [] rawData;
        Span<byte> span;

        public Packet (byte[] data, int length)
        {
            rawData = data;
            span = new Span<byte>(rawData, 0, length);

            var header = DecodeHeader (rawData[0]);
            PacketType = header.type;
            Channel = header.channel;
            Fragmented = header.fragmented;
            
            if (Fragmented) {
                FragmentId = BitConverter.ToUInt16(rawData, FRAGMENT_ID_OFFSET);
                FragmentIndex = rawData[FRAGMENT_INDEX_OFFSET];
                TotalFragments = rawData[FRAGMENT_TOTAL_OFFSET];
            } else {
                FragmentId = 0;
                FragmentIndex = 0;
                TotalFragments = 1;
            }
        }

        public Packet (PacketType packetType, Channel channel, ReadOnlySpan <byte> payload, bool fragmented = false)
        {
            byte header = EncodeHeader (packetType, channel, fragmented);
            int offset = fragmented ? FRAGMENT_PAYLOAD_OFFSET : PAYLOAD_OFFSET;
            rawData = new byte[payload.Length + offset];
            span = new Span<byte> (rawData);
            rawData[HEADER_OFFSET] = header;
            PacketType = packetType;
            Channel = channel;
            Fragmented = fragmented;
            FragmentId = 0;
            FragmentIndex = 0;
            TotalFragments = 1;
            Sequence = 0;
            payload.TryCopyTo (span.Slice (offset));
        }

        public Packet (PacketType packetType, Channel channel, ushort sequence, ReadOnlySpan <byte> payload, bool fragmented = false)
            : this (packetType, channel, payload, fragmented: fragmented)
        {
            Sequence = sequence;
        }
        
        public void SetFragmentInfo(ushort fragmentId, byte fragmentIndex, byte totalFragments)
        {
            if (!Fragmented) {
                throw new InvalidOperationException("Cannot set fragment info on non-fragmented packet");
            }
            FragmentId = fragmentId;
            FragmentIndex = fragmentIndex;
            TotalFragments = totalFragments;
            // Use BitConverter to properly handle ushort
            byte[] fragmentIdBytes = BitConverter.GetBytes(fragmentId);
            if (BitConverter.IsLittleEndian) {
                rawData[FRAGMENT_ID_OFFSET] = fragmentIdBytes[0];
                rawData[FRAGMENT_ID_OFFSET + 1] = fragmentIdBytes[1];
            } else {
                rawData[FRAGMENT_ID_OFFSET] = fragmentIdBytes[1];
                rawData[FRAGMENT_ID_OFFSET + 1] = fragmentIdBytes[0];
            }
            rawData[FRAGMENT_INDEX_OFFSET] = fragmentIndex;
            rawData[FRAGMENT_TOTAL_OFFSET] = totalFragments;
        }

        public byte Header { get { return span[HEADER_OFFSET]; }}

        public PacketType PacketType { get; private set; }

        public Channel Channel { get; private set; }

        public bool Fragmented { get; private set; }
        
        public ushort FragmentId { get; private set; }
        
        public byte FragmentIndex { get; private set; }
        
        public byte TotalFragments { get; private set; }

        public ushort Sequence {
            get { return DecodeSequence (); }
            set { EncodeSequence (value);}
        }

        public ReadOnlySpan<byte> Payload { 
            get { 
                int offset = Fragmented ? FRAGMENT_PAYLOAD_OFFSET : PAYLOAD_OFFSET;
                return span.Slice (offset); 
            }
        }

        public byte[] Data { get { return rawData; }}

        static byte EncodeHeader(PacketType type, Channel channel, bool fragmented = false)
        {   
            // Header Format
            // 0b00000000
            //   FCCTTTTT
            // C = Channel 2 bits
            // T = Packet TYpe 5 bits
            // F = Fragmented bit
            byte t = (byte)type;
            byte t1 = (byte)channel;
            byte header = (byte)(t1 << 5);
            byte f = (byte)(fragmented ? 1 << 7 : 0); 
            header = (byte)(f | header | t);
            return header;
        }

        static (PacketType type, Channel channel, bool fragmented) DecodeHeader (byte header)
        {
            return (type : (PacketType)(header & 0x1F), channel : (Channel)((header & 0x60) >> 5), fragmented: (bool)(((header & 0x80) >> 7) == 1));
        }

        void EncodeSequence(ushort sequence)
        {
            WriteLittleEndian (rawData, SEQUENCE_OFFSET, (short)sequence);
        }

        ushort DecodeSequence ()
        {
            return BitConverter.ToUInt16 (rawData, SEQUENCE_OFFSET);
        }

        public static void WriteLittleEndian(byte[] buffer, int offset, short data)
        {
#if BIGENDIAN
            buffer[offset + 1] = (byte)(data);
            buffer[offset    ] = (byte)(data >> 8);
#else
            buffer[offset] = (byte)(data);
            buffer[offset + 1] = (byte)(data >> 8);
#endif
        }

    }
}