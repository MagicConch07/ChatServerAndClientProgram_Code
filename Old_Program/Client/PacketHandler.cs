
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using ChatServer;
using ChatServer.ChatPacket;
using NetBase;

namespace BotClient
{
    public struct PacketHeader
    {
        public ushort PacketSize;
        public ushort PacketType;

        public const int HeaderSize = 4; // 2 bytes for PacketSize, 2 bytes for PacketType

        public static PacketHeader FromBytes(byte[] buffer)
        {
            PacketHeader header = new PacketHeader
            {
                PacketSize = BitConverter.ToUInt16(buffer, 0),
                PacketType = BitConverter.ToUInt16(buffer, 2)
            };
            return header;
        }

        public byte[] ToBytes()
        {
            byte[] buffer = new byte[HeaderSize];
            BitConverter.GetBytes(PacketSize).CopyTo(buffer, 0);
            BitConverter.GetBytes(PacketType).CopyTo(buffer, 2);
            return buffer;
        }
    }

    public class PacketHandler
    {
        private Dictionary<PacketType, Action<MemoryStream, Socket>> _packetHandlers;

        public PacketHandler()
        {
            _packetHandlers = new Dictionary<PacketType, Action<MemoryStream, Socket>>
            {
                { PacketType.MessageAck, HandleMsgAck },
                { PacketType.NickNameAck, HandleNickNameAck },
            };
        }

        public void HandlePacket(MemoryStream packetStream, Socket handler)
        {
            packetStream.Position = 0;
            PacketHeader header = DeserializeHeader(packetStream);

            if (_packetHandlers.TryGetValue((PacketType)header.PacketType, out Action<MemoryStream, Socket> handlerAction))
            {
                byte[] bodyData = new byte[packetStream.Length - PacketHeader.HeaderSize];
                packetStream.Read(bodyData, 0, bodyData.Length);
                MemoryStream bodyStream = new MemoryStream(bodyData);

                //Console.WriteLine($"[PACKET] Received packet: Type {header.PacketType}, Size {header.PacketSize}");
                handlerAction(bodyStream, handler);
            }
            else
            {
                Console.WriteLine($"[PACKET] Unknown packet type: {header.PacketType}");
            }
        }

        private PacketHeader DeserializeHeader(MemoryStream stream)
        {
            byte[] headerData = new byte[PacketHeader.HeaderSize];
            stream.Read(headerData, 0, PacketHeader.HeaderSize);

            return new PacketHeader
            {
                PacketSize = BitConverter.ToUInt16(headerData, 0),
                PacketType = BitConverter.ToUInt16(headerData, 2)
            };
        }

        private void HandleMsgAck(MemoryStream packetStream, Socket handler)
        {
            //Console.WriteLine("[PACKET] Handling MsgAck");

            byte[] packetData = packetStream.ToArray();
            //Console.WriteLine($"[PACKET] Packet data length: {packetData.Length} bytes");

            var packet = new PacketBase();
            packet.SetPacketData(packetData);

            MessageAck msgAck = new MessageAck();
            msgAck = packet.Read<MessageAck>();

            // whisperer
            if(msgAck.isWhisperer)
            {
                Program.ClearCurrentConsoleLine();
                Console.WriteLine($"[{msgAck.senderName}] -> [{msgAck.receiverName}] {msgAck.msg}");
                Console.Write("> ");
                return;
            }

            // message
            Program.ClearCurrentConsoleLine();
            Console.WriteLine($"[{msgAck.senderName}] {msgAck.msg}");
            Console.Write("> ");
        }

        private void HandleNickNameAck(MemoryStream packetStream, Socket handler)
        {
            byte[] packetData = packetStream.ToArray();

            var packet = new PacketBase();
            packet.SetPacketData(packetData);

            NickNameAck nickNameAck = new NickNameAck();
            nickNameAck = packet.Read<NickNameAck>();

            if(nickNameAck.successful)
            {
                Console.WriteLine("닉네임 설정 완료");
                Console.WriteLine("채팅 시작");
                Console.WriteLine("도움말: /help");
                Console.Write("> ");
                Program.SuccessfullName = true;
            }
            else
            {
                Console.Write("다시 이름을 입력하세요 : ");
            }
        }
    }
}
