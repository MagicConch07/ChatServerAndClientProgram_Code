using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ChatServer;
using ChatServer.ChatPacket;
using NetBase;

namespace BotClient
{
    internal class Program
    {
        public static bool SuccessfullName
        {
            get => _successfullName;
            set => _successfullName = true;
        }

        private static bool _successfullName = false;
        private static bool _running = true;
        private static readonly int _heartbeatInterval = 5000;
        private static Timer _heartbeatTimer;
        private static Timer _moveReqTimer;
        private static List<byte> _receivedDataBuffer = new List<byte>();
        private static PacketHandler _packetHandler;
        private static string _myName;

        private static void ProcessReceivedData(Socket socket, byte[] receivedData, int bytesReceived)
        {
            //Console.WriteLine($"[PACKET] Received {bytesReceived} bytes of data");
            _receivedDataBuffer.AddRange(receivedData.Take(bytesReceived));

            while (_receivedDataBuffer.Count >= PacketHeader.HeaderSize)
            {
                byte[] headerBytes = _receivedDataBuffer.Take(PacketHeader.HeaderSize).ToArray();
                PacketHeader receivedHeader = PacketHeader.FromBytes(headerBytes);

                if (_receivedDataBuffer.Count >= receivedHeader.PacketSize)
                {
                    byte[] packetData = _receivedDataBuffer.Take(receivedHeader.PacketSize).ToArray();

                    using (MemoryStream packetStream = new MemoryStream(packetData))
                    {
                        _packetHandler.HandlePacket(packetStream, socket);
                    }

                    _receivedDataBuffer.RemoveRange(0, receivedHeader.PacketSize);
                }
                else
                {
                    break;
                }
            }
        }

        private static void InitializeManagers()
        {
            _packetHandler = new PacketHandler();
        }

        static void Main(string[] args)
        {
            Console.WriteLine("[SYSTEM] Process start..");

            InitializeManagers();

            ConfigManager configManager = ConfigManager.Instance();
            string server = configManager.GetValue("ServerIP", "172.31.1.176");
            int port = configManager.GetIntValue("ServerPort", 11000);

            Console.WriteLine($"[SYSTEM] Server IP: {server}, Port: {port}");

            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnExit);

            try
            {
                IPAddress ipAddress = IPAddress.Parse(server);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                using (Socket sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    sender.Connect(remoteEP);
                    Console.WriteLine($"[PACKET] Socket connected to {sender.RemoteEndPoint.ToString()}");
                    Console.WriteLine("------------------------------");
                    Console.Write("닉네임을 설정하세요 : ");

                    sender.NoDelay = true;
                    sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 1000);
                    sender.Blocking = false;

                    int SendEscapeMoveCount = 0;

                    byte[] buffer = new byte[1024 * 16];
                    while (_running)
                    {
                        bool dataReceived = false;
                        do
                        {
                            if (sender.Poll(0, SelectMode.SelectRead))
                            {
                                int bytesReceived = sender.Receive(buffer);
                                if (bytesReceived > 0)
                                {
                                    ProcessReceivedData(sender, buffer, bytesReceived);
                                    dataReceived = true;
                                }
                                else
                                {
                                    _running = false;
                                    break;
                                }
                            }
                            else
                            {
                                dataReceived = false;
                            }
                        } while (dataReceived);

                        if (_running && !dataReceived)
                        {
                            Chat(sender);

                            Thread.Sleep(10);
                            if (SendEscapeMoveCount % 10 == 0)
                            {
                                SendEscapeMoveCount = 0;
                            }
                            SendEscapeMoveCount++;
                        }
                    }

                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] Exception: {e.ToString()}");
            }

            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }


        private static void OnExit(object sender, ConsoleCancelEventArgs args)
        {
            _running = false;
            args.Cancel = true;
            _heartbeatTimer?.Dispose();
            _moveReqTimer?.Dispose();
            Console.WriteLine("[SYSTEM] Exiting...");
        }

        public static void SendPacket(Socket socket, PacketType packetType, byte[] data)
        {
            try
            {
                ushort packetSize = (ushort)(PacketHeader.HeaderSize + data.Length);
                PacketHeader header = new PacketHeader { PacketSize = packetSize, PacketType = (ushort)packetType };
                byte[] headerBytes = header.ToBytes();

                byte[] packet = new byte[packetSize];
                headerBytes.CopyTo(packet, 0);
                data.CopyTo(packet, PacketHeader.HeaderSize);

                int bytesSend = socket.Send(packet);
                //Console.WriteLine($"[PACKET] Send {bytesSend} bytes to server, PacketType: {packetType.ToString()}");
                //Console.WriteLine("[PACKET] Packet content:");
                //Console.WriteLine("[PACKET] Header: " + BitConverter.ToString(headerBytes));
                //Console.WriteLine("[PACKET] Data: " + BitConverter.ToString(data));
                //Console.WriteLine("[PACKET] Full packet: " + BitConverter.ToString(packet));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error sending packet: {ex.Message}");
            }
        }

        private static void Chat(Socket sender)
        {
            if (Console.KeyAvailable)
            {
                // Set NickName
                if (!_successfullName)
                {
                    string nickName = Console.ReadLine();

                    var nickNamePacket = new PacketBase();
                    NickNameReq nickNameReq = new NickNameReq()
                    {
                        name = nickName
                    };
                    nickNamePacket.Write(nickNameReq);
                    SendPacket(sender, PacketType.NickNameReq, nickNamePacket.GetPacketData());

                    _myName = nickName;

                    return;
                }

                string chatMessage = Console.ReadLine();

                if (string.IsNullOrEmpty(chatMessage))
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    ClearCurrentConsoleLine();
                    Console.Write("> ");

                    return;
                }

                // command
                if (chatMessage[0] == '/')
                {
                    string commandLine = chatMessage.Substring(1).Trim();
                    string[] parts = commandLine.Split(new char[] { ' ' }, 2);
                    string command = parts[0].ToLower();

                    switch (command)
                    {
                        case "help":
                            Console.WriteLine("[명령어 목록]");
                            Console.WriteLine("도움말: /help, \n /w <대상> <메시지> | (가능 명령어 : whisper, ㅈ)");
                            break;

                        case "w":
                        case "ㅈ":
                        case "whisper":
                            if (parts.Length < 2)
                            {
                                Console.WriteLine("사용법: /w <대상> <메시지> | (가능 명령어 : whisper, ㅈ)");
                                break;
                            }

                            var whisperParts = parts[1].Split(new char[] { ' ' }, 2);
                            if (whisperParts.Length < 2)
                            {
                                Console.WriteLine("사용법: /w <대상> <메시지> | (가능 명령어 : whisper, ㅈ)");
                                break;
                            }

                            string reciverUserName = whisperParts[0];
                            string whisperMessage = whisperParts[1];

                            if (reciverUserName == _myName)
                            {
                                Console.WriteLine("잘못된 사용법");
                                Console.WriteLine("사용법: /w <대상> <메시지> | (가능 명령어 : whisper, ㅈ)");
                            }

                            var whisperMsgPacket = new PacketBase();
                            MessageReq whisperMsgReq = new MessageReq
                            {
                                senderName = _myName,
                                receiverName = reciverUserName,
                                msg = whisperMessage,
                                isWhisperer = true
                            };
                            whisperMsgPacket.Write(whisperMsgReq);
                            SendPacket(sender, PacketType.MessageReq, whisperMsgPacket.GetPacketData());
                            break;

                        default:
                            Console.WriteLine($"알 수 없는 명령어: {command}");
                            Console.WriteLine("[명령어 목록]");
                            Console.WriteLine("도움말: /help, \n /w <대상> <메시지> | (가능 명령어 : whisper, ㅈ)");
                            break;
                    }

                    Console.Write("> ");
                    return;
                }
                else
                {
                    // message
                    var msgPacket = new PacketBase();
                    MessageReq msgReq = new MessageReq
                    {
                        senderName = _myName,
                        receiverName = "none",
                        msg = chatMessage,
                    };
                    msgPacket.Write(msgReq);

                    SendPacket(sender, PacketType.MessageReq, msgPacket.GetPacketData());
                }

                Console.SetCursorPosition(0, Console.CursorTop - 1);
                ClearCurrentConsoleLine();
            }
        }

        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, currentLineCursor);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }
    }
}
