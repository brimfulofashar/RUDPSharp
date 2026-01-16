using System;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;
using RUDPSharp;

namespace RUDPSharp.Tests
{
    public class ChannelTests
    {
        IPEndPoint EndPoint = new IPEndPoint(IPAddress.Any, 8000);

        [Test]
        [Ignore ("Not implemented")]
        public void TestUnreliableChannelHandlesFragmentedPackages ()
        {
            var channel = new UnreliableChannel ();
            channel.QueueIncomingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,1, new byte[10] {1,2,3,4,5,6,7,8,9,0}));
            channel.QueueIncomingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,3, new byte[5] {1,2,3,4,5}, fragmented: true));

            PendingPacket packet;
            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet), "Should have got packet 1");
            Assert.IsNotNull (packet, "Packet 1 should not be null.");
            Assert.AreEqual (1, packet.Sequence, $"Packet sequence should be 1 but was {packet.Sequence}");
            Assert.AreEqual (new byte[10] {1,2,3,4,5,6,7,8,9,0}, packet.Data, "Packet 1 data did not match.");

            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet));
            Assert.IsNull (packet, "We should not have got the fragmented packet yet.");

            channel.QueueIncomingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,2, new byte[5] {6,7,8,9,0}, fragmented: true));
            channel.QueueIncomingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,4, new byte[10] {1,2,3,4,5,6,7,8,9,0}));

            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet));
            Assert.IsNotNull (packet);
            Assert.AreEqual (2, packet.Sequence);

            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet), "Should have got packet 1");
            Assert.IsNotNull (packet, "Packet 4 should not be null.");
            Assert.AreEqual (4, packet.Sequence, $"Packet sequence should be 4 but was {packet.Sequence}");
            Assert.AreEqual (new byte[10] {1,2,3,4,5,6,7,8,9,0}, packet.Data, "Packet 4 data did not match.");

            
        }

        [Test]
        public void TestUnreliableChannelDoesNotDiscardsOldMessages()
        {
            // Create 3 packets, add them in the order 1, 3, 2. 
            // 2 should be discarded.
            var channel = new UnreliableChannel ();
            channel.QueueIncomingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,1, new byte[10] {1,2,3,4,5,6,7,8,9,0}));
            channel.QueueIncomingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,3, Array.Empty<byte> ()));
            channel.QueueIncomingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,2, Array.Empty<byte> ()));

            PendingPacket packet;
            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet), "Should have got packet 1");
            Assert.IsNotNull (packet, "Packet 1 should not be null.");
            Assert.AreEqual (1, packet.Sequence, $"Packet sequence should be 1 but was {packet.Sequence}");
            Assert.AreEqual (new byte[10] {1,2,3,4,5,6,7,8,9,0}, packet.Data, "Packet 1 data did not match.");

            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet));
            Assert.IsNotNull (packet);
            Assert.AreEqual (3, packet.Sequence);

            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet));
            Assert.IsNotNull (packet);
            Assert.AreEqual (2, packet.Sequence);
        }

        [Test]
        public void TestInOrderChannelDiscardsOldMessages()
        {
            // Create 3 packets, add them in the order 1, 3, 2. 
            // 2 should be discarded.
            var channel = new InOrderChannel ();
            channel.QueueIncomingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,1, Array.Empty<byte> ()));
            channel.QueueIncomingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,3, new byte[10] {1,2,3,4,5,6,7,8,9,0}));
            channel.QueueIncomingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,2, Array.Empty<byte> ()));

            PendingPacket packet;
            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet), "Packet 1 was not received.");
            Assert.IsNotNull (packet);
            Assert.AreEqual (1, packet.Sequence);

            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet), "Packet 3 was not received.");
            Assert.IsNotNull (packet);
            Assert.AreEqual (3, packet.Sequence);
            Assert.AreEqual (new byte[10] {1,2,3,4,5,6,7,8,9,0}, packet.Data);

            Assert.IsFalse (channel.TryGetNextIncomingPacket (out packet), "Packet 2 was not ignored.");
            Assert.IsNull (packet);
        }

        [Test]
        public void TestReliableChannelDoesNotDiscardsOldMessages()
        {
            // Create 3 packets, add them in the order 1, 3, 2. 
            // 2 should be discarded 
            var channel = new ReliableChannel ();

            channel.QueueIncomingPacket(EndPoint, new Packet (PacketType.Connect, Channel.Reliable,1, Array.Empty<byte> ()));
            channel.QueueIncomingPacket(EndPoint, new Packet (PacketType.Connect, Channel.Reliable,3, new byte[10] {1,2,3,4,5,6,7,8,9,0}));
            channel.QueueIncomingPacket(EndPoint, new Packet (PacketType.Connect, Channel.Reliable,2, Array.Empty<byte> ()));

            PendingPacket packet;
            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet));
            Assert.IsNotNull (packet);
            Assert.AreEqual (1, packet.Sequence);

            Assert.IsTrue (channel.TryGetNextIncomingPacket (out packet));
            Assert.IsNotNull (packet);
            Assert.AreEqual (3, packet.Sequence);
            Assert.AreEqual (new byte[10] {1,2,3,4,5,6,7,8,9,0}, packet.Data);

            Assert.IsFalse(channel.TryGetNextIncomingPacket(out packet));
            Assert.IsNull(packet);
        }

        [Test]
        public void TestReliableInOrderChannelDiscardsOldMessages()
        {
            // Create 3 packets, add them in the order 1, 3, 2. 
            // 2 should be delivered as long as it did not expire
            var channel = new ReliableInOrderChannel();

            channel.QueueIncomingPacket(EndPoint, new Packet(PacketType.Connect, Channel.Reliable, 1, Array.Empty<byte> ()));
            channel.QueueIncomingPacket(EndPoint, new Packet(PacketType.Connect, Channel.Reliable, 3, new byte[10] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 }));
            channel.QueueIncomingPacket(EndPoint, new Packet(PacketType.Connect, Channel.Reliable, 2, Array.Empty<byte> ()));
            channel.QueueIncomingPacket(EndPoint, new Packet(PacketType.Connect, Channel.Reliable, 4, Array.Empty<byte> ()));

            PendingPacket packet;
            Assert.IsTrue(channel.TryGetNextIncomingPacket(out packet), "Should have got packet 1");
            Assert.IsNotNull(packet);
            Assert.AreEqual(1, packet.Sequence);

            Assert.IsTrue(channel.TryGetNextIncomingPacket(out packet), "Should have got packet 2");
            Assert.IsNotNull(packet);
            Assert.AreEqual(2, packet.Sequence);

            Assert.IsTrue(channel.TryGetNextIncomingPacket(out packet), "Should have got packet 3");
            Assert.IsNotNull(packet);
            Assert.AreEqual(3, packet.Sequence);
            Assert.AreEqual(new byte[10] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 }, packet.Data);

            Assert.IsTrue(channel.TryGetNextIncomingPacket(out packet), "Should have got packet 4");
            Assert.IsNotNull(packet);
            Assert.AreEqual(4, packet.Sequence);

            Assert.IsFalse(channel.TryGetNextIncomingPacket(out packet), "Should not have got a packet");
            Assert.IsNull(packet);
        }

        [Test]
        public void TestInOrderChannelHandlesSequenceWrapping ()
        {
            var rnd = new Random ();
            var outChannel = new InOrderChannel ();
            var inChannel = new InOrderChannel ();
            int[] sequences = [1, 534, 5346, 15346, 25676, 35246, 45646, 55366, 65532, 50, 4560];
            foreach (var sequence in sequences) {
                PendingPacket outPacket;
                ushort expectedSequence = (ushort)((sequence % (ushort.MaxValue)));
                do {
                    // get a bunch of packets and discard them
                    outChannel.QueueOutgoingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,0, Array.Empty<byte> ()));
                    Assert.IsTrue (outChannel.TryGetNextOutgoingPacket (out outPacket));
                    Assert.IsTrue (outPacket.Sequence < (ushort.MaxValue -1));
                } while (outPacket.Sequence != expectedSequence);

                inChannel.QueueIncomingPacket (EndPoint, new Packet (outPacket.PacketType, Channel.None, (ushort)outPacket.Sequence, outPacket.Data, false));
                Assert.IsTrue (inChannel.TryGetNextIncomingPacket (out PendingPacket packet), $"Should have got a packet for {sequence}");
                Assert.AreEqual (expectedSequence, packet.Sequence, $"Packet sequence should be {expectedSequence} but was {packet.Sequence}");
            }
        }

        [Test]
        public void TestReliableChannelResends () {
            var channel = new ReliableChannel ();

            channel.QueueOutgoingPacket(EndPoint, new Packet(PacketType.Connect, Channel.Reliable, 1, Array.Empty<byte> ()));

            PendingPacket[] packets = channel.GetPendingOutgoingPackets().ToArray ();
            Assert.IsNotEmpty (packets);
            Assert.AreEqual(1, packets[0].Sequence);

            channel.QueueIncomingPacket(EndPoint, new Packet (PacketType.Ack, Channel.Reliable, (ushort)packets[0].Sequence, Array.Empty<byte> ()));
            packets = channel.GetPendingIncomingPackets().ToArray ();
            Assert.IsEmpty (packets);

            channel.QueueOutgoingPacket(EndPoint, new Packet(PacketType.Connect, Channel.Reliable, 2, Array.Empty<byte> ()));
            packets = channel.GetPendingOutgoingPackets().ToArray ();
            Assert.IsNotEmpty (packets);
            Assert.AreEqual(2, packets[0].Sequence);

            Thread.Sleep (600);

            packets = channel.GetPendingOutgoingPackets().ToArray ();
            Assert.IsNotEmpty (packets);
            Assert.AreEqual(2, packets[0].Sequence);

            Thread.Sleep (600);

            packets = channel.GetPendingOutgoingPackets().ToArray ();
            Assert.IsNotEmpty (packets);
            Assert.AreEqual(2, packets[0].Sequence);

            channel.QueueIncomingPacket(EndPoint, new Packet (PacketType.Ack, Channel.Reliable, (ushort)packets[0].Sequence, Array.Empty<byte> ()));
            packets = channel.GetPendingIncomingPackets().ToArray ();
            Assert.IsEmpty (packets);

            packets = channel.GetPendingOutgoingPackets().ToArray ();
            Assert.IsEmpty (packets);
        }

        [Test]
        public void TestReliableInOrderChannelHandlesSequenceWrapping ()
        {
            var rnd = new Random ();
            var outChannel = new ReliableInOrderChannel ();
            var inChannel = new ReliableInOrderChannel ();
            int[] sequences = [1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 10, 11];
            foreach (var sequence in sequences) {
                PendingPacket outPacket;
                ushort expectedSequence = (ushort)((sequence % (ushort.MaxValue)));
                do {
                    // get a bunch of packets and discard them
                    outChannel.QueueOutgoingPacket (EndPoint, new Packet (PacketType.UnconnectedMessage, Channel.None,0, Array.Empty<byte> ()));
                    Assert.IsTrue (outChannel.TryGetNextOutgoingPacket (out outPacket));
                    Assert.IsTrue (outPacket.Sequence < (ushort.MaxValue -1));
                } while (outPacket.Sequence != expectedSequence);

                Thread.Sleep (600);
                inChannel.QueueIncomingPacket (EndPoint, new Packet (outPacket.PacketType, Channel.None, (ushort)outPacket.Sequence, outPacket.Data, false));
                Assert.IsTrue (inChannel.TryGetNextIncomingPacket (out PendingPacket packet), $"Should have got a packet for {sequence}");
                Assert.AreEqual (expectedSequence, packet.Sequence, $"Packet sequence should be {expectedSequence} but was {packet.Sequence}");
            }
        }

        [Test]
        public void TestFragmentationBoundaryNonFragmented()
        {
            // Test packet just below fragmentation threshold (1016 bytes)
            // Should NOT be fragmented (threshold is 1017 bytes)
            var channel = new UnreliableChannel();
            byte[] payload = new byte[1016];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            var packet = new Packet(PacketType.Data, Channel.None, 1, payload);
            var pendingPacket = channel.QueueOutgoingPacket(EndPoint, packet);

            Assert.IsNotNull(pendingPacket);
            
            // Should get exactly one packet (not fragmented)
            var outgoingPackets = channel.GetPendingOutgoingPackets().ToArray();
            Assert.AreEqual(1, outgoingPackets.Length, "Should have exactly 1 packet (not fragmented)");
            Assert.AreEqual(1016, outgoingPackets[0].Data.Length - 3, "Payload size should be 1016 bytes");
        }

        [Test]
        public void TestFragmentationBoundaryExact()
        {
            // Test packet at exact fragmentation threshold (1017 bytes)
            // Should NOT be fragmented (threshold is >1017, not >=1017)
            var channel = new UnreliableChannel();
            byte[] payload = new byte[1017];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            var packet = new Packet(PacketType.Data, Channel.None, 1, payload);
            var pendingPacket = channel.QueueOutgoingPacket(EndPoint, packet);

            Assert.IsNotNull(pendingPacket);
            
            // Should get exactly one packet (not fragmented)
            var outgoingPackets = channel.GetPendingOutgoingPackets().ToArray();
            Assert.AreEqual(1, outgoingPackets.Length, "Should have exactly 1 packet at boundary");
        }

        [Test]
        public void TestFragmentationBoundaryJustAbove()
        {
            // Test packet just above fragmentation threshold (1018 bytes)
            // Should be fragmented into 2 packets
            var channel = new UnreliableChannel();
            byte[] payload = new byte[1018];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            var packet = new Packet(PacketType.Data, Channel.None, 1, payload);
            var pendingPacket = channel.QueueOutgoingPacket(EndPoint, packet);

            Assert.IsNotNull(pendingPacket);
            
            // Should get exactly two fragments
            var outgoingPackets = channel.GetPendingOutgoingPackets().ToArray();
            Assert.AreEqual(2, outgoingPackets.Length, "Should have 2 fragments for 1018 bytes");
        }

        [Test]
        public void TestFragmentationReassemblyBoundary()
        {
            // Test that fragments at boundary are correctly reassembled
            var channel = new UnreliableChannel();
            byte[] payload = new byte[1018];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            var outPacket = new Packet(PacketType.Data, Channel.None, 1, payload);
            channel.QueueOutgoingPacket(EndPoint, outPacket);
            
            var outgoingPackets = channel.GetPendingOutgoingPackets().ToArray();
            Assert.AreEqual(2, outgoingPackets.Length);

            // Simulate receiving the fragments
            var receiveChannel = new UnreliableChannel();
            foreach (var fragment in outgoingPackets)
            {
                var receivedPacket = new Packet(fragment.Data, fragment.Data.Length);
                receiveChannel.QueueIncomingPacket(EndPoint, receivedPacket);
            }

            // Should get one reassembled packet
            var incomingPackets = receiveChannel.GetPendingIncomingPackets().ToArray();
            Assert.AreEqual(1, incomingPackets.Length, "Should have 1 reassembled packet");
            Assert.AreEqual(1018, incomingPackets[0].Data.Length, "Reassembled packet should be 1018 bytes");
            
            // Verify data integrity
            for (int i = 0; i < payload.Length; i++)
            {
                Assert.AreEqual((byte)(i % 256), incomingPackets[0].Data[i], $"Data mismatch at index {i}");
            }
        }

        [Test]
        public void TestFragmentationMultipleFragments()
        {
            // Test packet that requires multiple fragments (2048 bytes = 3 fragments)
            var channel = new UnreliableChannel();
            byte[] payload = new byte[2048];
            var rnd = new Random(12345);
            rnd.NextBytes(payload);

            var packet = new Packet(PacketType.Data, Channel.None, 1, payload);
            channel.QueueOutgoingPacket(EndPoint, packet);
            
            var outgoingPackets = channel.GetPendingOutgoingPackets().ToArray();
            // 2048 bytes / 1017 bytes per fragment = 3 fragments (1017 + 1017 + 14)
            Assert.AreEqual(3, outgoingPackets.Length, "Should have 3 fragments for 2048 bytes");

            // Simulate receiving the fragments in order
            var receiveChannel = new UnreliableChannel();
            foreach (var fragment in outgoingPackets)
            {
                var receivedPacket = new Packet(fragment.Data, fragment.Data.Length);
                receiveChannel.QueueIncomingPacket(EndPoint, receivedPacket);
            }

            var incomingPackets = receiveChannel.GetPendingIncomingPackets().ToArray();
            Assert.AreEqual(1, incomingPackets.Length, "Should have 1 reassembled packet");
            Assert.AreEqual(2048, incomingPackets[0].Data.Length, "Reassembled packet should be 2048 bytes");
            Assert.AreEqual(payload, incomingPackets[0].Data, "Reassembled data should match original");
        }

        [Test]
        public void TestFragmentationOutOfOrderReassembly()
        {
            // Test that fragments received out of order are correctly reassembled
            var channel = new UnreliableChannel();
            byte[] payload = new byte[3000];
            var rnd = new Random(54321);
            rnd.NextBytes(payload);

            var packet = new Packet(PacketType.Data, Channel.None, 1, payload);
            channel.QueueOutgoingPacket(EndPoint, packet);
            
            var outgoingPackets = channel.GetPendingOutgoingPackets().ToArray();
            Assert.AreEqual(3, outgoingPackets.Length, "Should have 3 fragments for 3000 bytes");

            // Simulate receiving the fragments OUT OF ORDER (2, 0, 1)
            var receiveChannel = new UnreliableChannel();
            var receivedPacket2 = new Packet(outgoingPackets[2].Data, outgoingPackets[2].Data.Length);
            receiveChannel.QueueIncomingPacket(EndPoint, receivedPacket2);

            var receivedPacket0 = new Packet(outgoingPackets[0].Data, outgoingPackets[0].Data.Length);
            receiveChannel.QueueIncomingPacket(EndPoint, receivedPacket0);

            var receivedPacket1 = new Packet(outgoingPackets[1].Data, outgoingPackets[1].Data.Length);
            receiveChannel.QueueIncomingPacket(EndPoint, receivedPacket1);

            var incomingPackets = receiveChannel.GetPendingIncomingPackets().ToArray();
            Assert.AreEqual(1, incomingPackets.Length, "Should have 1 reassembled packet");
            Assert.AreEqual(3000, incomingPackets[0].Data.Length, "Reassembled packet should be 3000 bytes");
            Assert.AreEqual(payload, incomingPackets[0].Data, "Reassembled data should match original despite out-of-order receipt");
        }

        [Test]
        public void TestFragmentationLargePacket()
        {
            // Test large packet (4096 bytes = 5 fragments)
            var channel = new UnreliableChannel();
            byte[] payload = new byte[4096];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            var packet = new Packet(PacketType.Data, Channel.None, 1, payload);
            channel.QueueOutgoingPacket(EndPoint, packet);
            
            var outgoingPackets = channel.GetPendingOutgoingPackets().ToArray();
            // 4096 / 1017 = ~5 fragments
            Assert.AreEqual(5, outgoingPackets.Length, "Should have 5 fragments for 4096 bytes");

            // Verify reassembly
            var receiveChannel = new UnreliableChannel();
            foreach (var fragment in outgoingPackets)
            {
                var receivedPacket = new Packet(fragment.Data, fragment.Data.Length);
                receiveChannel.QueueIncomingPacket(EndPoint, receivedPacket);
            }

            var incomingPackets = receiveChannel.GetPendingIncomingPackets().ToArray();
            Assert.AreEqual(1, incomingPackets.Length);
            Assert.AreEqual(4096, incomingPackets[0].Data.Length);
            
            for (int i = 0; i < payload.Length; i++)
            {
                Assert.AreEqual((byte)(i % 256), incomingPackets[0].Data[i], $"Data mismatch at index {i}");
            }
        }

        [Test]
        public void TestFragmentationMaxFragmentCount()
        {
            // Test approaching maximum fragment count (255 fragments)
            // 255 * 1017 = 259335 bytes
            var channel = new UnreliableChannel();
            int payloadSize = 255 * 1017; // Exactly 255 fragments
            byte[] payload = new byte[payloadSize];
            
            // Fill with pattern for verification
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            var packet = new Packet(PacketType.Data, Channel.None, 1, payload);
            var pendingPacket = channel.QueueOutgoingPacket(EndPoint, packet);

            Assert.IsNotNull(pendingPacket);
            var outgoingPackets = channel.GetPendingOutgoingPackets().ToArray();
            Assert.AreEqual(255, outgoingPackets.Length, "Should have exactly 255 fragments");

            // Verify reassembly
            var receiveChannel = new UnreliableChannel();
            foreach (var fragment in outgoingPackets)
            {
                var receivedPacket = new Packet(fragment.Data, fragment.Data.Length);
                receiveChannel.QueueIncomingPacket(EndPoint, receivedPacket);
            }

            var incomingPackets = receiveChannel.GetPendingIncomingPackets().ToArray();
            Assert.AreEqual(1, incomingPackets.Length);
            Assert.AreEqual(payloadSize, incomingPackets[0].Data.Length);
        }

        [Test]
        public void TestFragmentationExceedsMaxFragmentCount()
        {
            // Test that exceeding maximum fragment count throws exception
            var channel = new UnreliableChannel();
            int payloadSize = 256 * 1017; // Would require 256 fragments (exceeds 255 max)
            byte[] payload = new byte[payloadSize];

            bool exceptionThrown = false;
            try
            {
                var packet = new Packet(PacketType.Data, Channel.None, 1, payload);
                channel.QueueOutgoingPacket(EndPoint, packet);
            }
            catch (InvalidOperationException)
            {
                exceptionThrown = true;
            }
            
            Assert.IsTrue(exceptionThrown, "Should throw InvalidOperationException when exceeding max fragment count");
        }
    }
}