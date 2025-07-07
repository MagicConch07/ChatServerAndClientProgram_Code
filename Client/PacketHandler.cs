
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using MKChatServer;
using MKChatServer.ChatPacket;
using NetBase;
using UnityEngine;

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
                handlerAction(bodyStream, handler);
            }
            else
            {
#if UNITY_EDITOR
                Debug.LogError($"[PACKET] Unknown packet type: {header.PacketType}");
#endif
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
            byte[] packetData = packetStream.ToArray();

            var packet = new PacketBase();
            packet.SetPacketData(packetData);

            MessageAck msgAck = new MessageAck();
            msgAck = packet.Read<MessageAck>();

            if (msgAck.msgType == MsgType.Msg)
            {
                // TODO : 메세지 처리 변경
            }
            else if (msgAck.msgType == MsgType.Whisper)
            {
                // TODO : 귓속말 처리 변경
            }
        }

        private void HandleNickNameAck(MemoryStream packetStream, Socket handler)
        {
            byte[] packetData = packetStream.ToArray();

            var packet = new PacketBase();
            packet.SetPacketData(packetData);

            NickNameAck nickNameAck = new NickNameAck();
            nickNameAck = packet.Read<NickNameAck>();

            if (nickNameAck.successful)
            {
                // TODO : 여기서 닉네임 검증 로직 변경하기
            }
            else
            {
                // TODO : 닉네임 실패
            }
        }
    }
}
