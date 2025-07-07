using NetBase;
using System.Runtime.CompilerServices;
using System.Net.Sockets;
using ChatServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ChatServer.ChatPacket;

namespace Process
{
    // Packet Handler
    public class PacketHandler
    {
        int updateCount = 0;
        public static long GetTickCount64()
        {
            return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }
        private void LogError(string message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            string fileName = System.IO.Path.GetFileName(file);
            Console.WriteLine($"Error: {message} (in {fileName}, line {line})");
        }

        private Dictionary<PacketType, Action<MemoryStream, Socket, int, long>> _packetHandlers;
        public PacketHandler(Action<PacketType, long> updateStatsMethod)
        {
            this.updatePacketStats = updateStatsMethod;
            _packetHandlers = new Dictionary<PacketType, Action<MemoryStream, Socket, int, long>>
            {
                { PacketType.MessageReq, HandleMsgReq },
                { PacketType.NickNameReq, HandleNickNameReq },
            };

            foreach (PacketType packetType in Enum.GetValues(typeof(PacketType)))
            {
                Program.packetStats[packetType] = new Program.PacketStats();
            }
        }

        private Action<PacketType, long> updatePacketStats;

        public void HandlePacket(MemoryStream packetStream, Socket handler, int ClientId, long PacketIndex)
        {
            packetStream.Position = 0;
            PacketHeader header = DeserializeHeader(packetStream);
            PacketType packetType = (PacketType)header.Type;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (_packetHandlers.TryGetValue(packetType, out Action<MemoryStream, Socket, int, long> handlerAction))
            {
                byte[] bodyData = new byte[packetStream.Length - PacketHeader.HeaderSize];
                packetStream.Read(bodyData, 0, bodyData.Length);

                PoolableMemoryStream pooledBodyStream = Program.PacketHandlerMemoryStreamPool.Get();
                try
                {
                    // ✅ 중요: bodyData를 pooledBodyStream에 설정
                    pooledBodyStream.SetData(bodyData);
                    Console.WriteLine($"[PACKET] Received packet: Type {header.Type}, Size {header.Type}");

                    handlerAction(pooledBodyStream.Stream, handler, ClientId, PacketIndex);
                }
                finally
                {
                    Program.PacketHandlerMemoryStreamPool.Return(pooledBodyStream);
                }
            }
            else
            {
                Console.WriteLine($"Unknown packet type: {packetType}");
            }

            stopwatch.Stop();
            Program.UpdatePacketStats(packetType, stopwatch.ElapsedMilliseconds);
        }

        public delegate void UpdateStatsDelegate(PacketType packetType, long processingTime);
        public UpdateStatsDelegate UpdatePacketStats { get; set; }

        private PacketHeader DeserializeHeader(MemoryStream stream)
        {
            byte[] sizeBytes = new byte[2];
            byte[] typeBytes = new byte[2];
            stream.Read(sizeBytes, 0, 2);
            stream.Read(typeBytes, 0, 2);

            return new PacketHeader
            {
                Size = BitConverter.ToUInt16(sizeBytes, 0),
                Type = (ushort)(PacketType)BitConverter.ToUInt16(typeBytes, 0)
            };
        }

        public void HandleUpdate()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int threadId = Thread.CurrentThread.ManagedThreadId;
            bool logFlag = false;

            if (updateCount % 50 == 0)
            {
                Console.WriteLine($"Current Thread ID: {threadId} Handling HandleUpdate");
                logFlag = true;
            }

            if (updateCount > 50)
                updateCount = 0;
            updateCount++;
            stopwatch.Stop();
            
            Program.UpdatePacketStats(PacketType.None, stopwatch.ElapsedMilliseconds);
        }

        private void HandleMsgReq(MemoryStream packetStream, Socket handler, int clientId, long packetIndex)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            Log.Instance.FileLog(Log.LogId.PACKET, Log.LogLevel.TRC_DATA, $"Current Thread ID: {threadId} Handling MsgReq [{packetIndex}]");

            var packet = new PacketBase();

            byte[] packetData = packetStream.ToArray();
            packet.SetPacketData(packetData);

            MessageReq messageReqData = new MessageReq();
            messageReqData = packet.Read<MessageReq>();

            string senderName = "???";
            NickNameManager.Instance.TryGetNickName(clientId, out senderName);

            if (messageReqData.senderName == messageReqData.receiverName)
            {
                Log.Instance.FileLog(Log.LogId.PACKET, Log.LogLevel.TRC_WARNING, $"잘못된 귓속말 이름 : {messageReqData.senderName}");
                Console.WriteLine("잘못된 요청");
                return;
            }

            MessageAck msgAck = new MessageAck()
            {
                msg = messageReqData.msg,
                senderName = senderName,
                receiverName = messageReqData.receiverName,
                isWhisperer = messageReqData.isWhisperer
            };

            Console.WriteLine($"[{msgAck.senderName}] : {msgAck.msg}");

            var msg_packet = new NetBase.PacketBase();
            msg_packet.Write(msgAck);

            if(messageReqData.isWhisperer)
            {
                // TODO : Clinet ID 오류 처리
                if(NickNameManager.Instance.TryGetClientId(messageReqData.receiverName, out int id))
                {
                    Program.SendPacket(handler, PacketType.MessageAck, msg_packet.GetPacketData());
                    Program.SendPacket(SocketManager.GetSocket(id), PacketType.MessageAck, msg_packet.GetPacketData());
                }
                return;
            }
            SocketManager.BroadcastToAll(PacketType.MessageAck, msg_packet.GetPacketData());
        }

        private void HandleNickNameReq(MemoryStream packetStream, Socket handler, int clientId, long packetIndex)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            Log.Instance.FileLog(Log.LogId.PACKET, Log.LogLevel.TRC_DATA, $"Current Thread ID: {threadId} Handling MsgReq [{packetIndex}]");

            var packet = new PacketBase();

            byte[] packetData = packetStream.ToArray();
            packet.SetPacketData(packetData);

            NickNameReq nameReqData = new NickNameReq();
            nameReqData = packet.Read<NickNameReq>();

            NickNameAck nickNameAck = new NickNameAck();

            if (NickNameManager.Instance.TryAddNickName(clientId, nameReqData.name))
            {
                // Success
                nickNameAck.successful = true;
            }
            else
            {
                // Failure
                nickNameAck.successful = false;
            }

            var nickName_packet = new NetBase.PacketBase();
            nickName_packet.Write(nickNameAck);

            Program.SendPacket(handler, PacketType.NickNameAck, nickName_packet.GetPacketData());
        }
    }
}
