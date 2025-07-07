﻿using MKChatServer;
using NetBase;
using Process;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;

namespace ChatServer
{
    /// <summary>
    /// IO 이벤트의 타입을 정의하는 열거형
    /// IOCP 워커 스레드가 처리할 이벤트의 종류를 구분
    /// </summary>
    public enum IOEventType
    {
        RemovePc,    // PC(Player Character) 제거 이벤트
        Update,      // 주기적 업데이트 이벤트
        ProcessPacket, // 패킷 처리 이벤트
    }

    /// <summary>
    /// PC 제거 이벤트에 필요한 데이터를 담는 구조체
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RemovePcEventArgs
    {
        public int ClientId; // 제거할 클라이언트의 ID
    }

    /// <summary>
    /// 패킷 처리에 필요한 정보를 담는 구조체
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PacketInfo
    {
        public Socket SocketHandle;     // 클라이언트 소켓
        public IntPtr DataPtr;          // 패킷 데이터 포인터
        public int DataSize;            // 패킷 크기
        public int ClientId;            // 클라이언트 ID
        public long PacketIndex;        // 패킷 순서 번호
    }

    /// <summary>
    /// 패킷 처리를 위한 유틸리티 클래스
    /// </summary>
    public static class NetworkProcessor
    {
        /// <summary>
        /// 모든 완전한 패킷 처리
        /// </summary>
        public static void ProcessAvailablePackets(ClientConnection connection, Socket handler)
        {
            while (TryProcessNextPacket(connection, handler))
            { }
        }

        /// <summary>
        /// 다음 완전한 패킷을 처리하고 성공 여부 반환
        /// </summary>
        private static bool TryProcessNextPacket(ClientConnection connection, Socket handler)
        {
            // 패킷 헤더 크기 (2바이트: 크기, 2바이트: 타입)
            const int HEADER_SIZE = 4;
            const int SIZE_FIELD_SIZE = 2;

            lock (connection._lock)
            {
                // 1. 헤더를 읽기에 충분한 데이터가 있는지 확인
                if (connection._dataBuffer.Length < SIZE_FIELD_SIZE)
                {
                    return false; // 더 많은 데이터 필요
                }

                // 2. 패킷 크기 읽기
                connection._dataBuffer.Position = 0;
                byte[] sizeBytes = new byte[SIZE_FIELD_SIZE];
                connection._dataBuffer.Read(sizeBytes, 0, SIZE_FIELD_SIZE);
                ushort packetSize = BitConverter.ToUInt16(sizeBytes, 0);

                // 3. 완전한 패킷이 도착했는지 확인
                if (connection._dataBuffer.Length < packetSize)
                {
                    return false; // 더 많은 데이터 필요
                }

                Log.Instance.FileLog(Log.LogId.NET, Log.LogLevel.TRC_DATA, $"완전한 패킷 수신: {packetSize} 바이트");

                // 4. 패킷 추출
                connection._dataBuffer.Position = 0;
                byte[] packetData = new byte[packetSize];
                connection._dataBuffer.Read(packetData, 0, packetSize);

                Log.Instance.FileLog(Log.LogId.NET, Log.LogLevel.TRC_DATA, $"packetData Length: {packetData.Length} 바이트");

                // 5. 버퍼에서 처리된 데이터 제거
                byte[] remainingData = new byte[connection._dataBuffer.Length - packetSize];
                if (remainingData.Length > 0)
                {
                    connection._dataBuffer.Read(remainingData, 0, remainingData.Length);

                    // 새 버퍼 생성 및 남은 데이터 복사
                    MemoryStream newStream = new MemoryStream();
                    newStream.Write(remainingData, 0, remainingData.Length);

                    // 이전 스트림 정리하고 새 스트림으로 교체
                    MemoryStream oldStream = connection._dataBuffer;
                    connection._dataBuffer = newStream;
                }
                else
                {
                    // 남은 데이터 없음 - 버퍼 초기화
                    connection._dataBuffer.SetLength(0);
                    connection._dataBuffer.Position = 0;
                }

                // 6. 패킷 처리를 위한 정보 구성
                IntPtr packetDataPtr = Marshal.AllocHGlobal(packetData.Length);
                Marshal.Copy(packetData, 0, packetDataPtr, packetData.Length);
                long packetIndex = Interlocked.Increment(ref Program.currentPacketIndex);

                PacketInfo packetInfo = new PacketInfo
                {
                    SocketHandle = handler,
                    DataPtr = packetDataPtr,
                    DataSize = packetData.Length,
                    ClientId = connection.ClientId,
                    PacketIndex = packetIndex
                };

                // 7. IOCP 이벤트 등록
                IntPtr packetInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(packetInfo));
                Marshal.StructureToPtr(packetInfo, packetInfoPtr, false);
                Program.PostProcessPacketEvent(packetInfoPtr);

                return true; // 패킷을 성공적으로 처리
            }
        }
    }

    internal class Program
    {
        // 상수 정의
        public const int MAX_PACKET_SIZE = 8192 * 2;   // 최대 패킷 크기 (16KB)
        private const int RECV_THREAD_COUNT = 8;        // 수신 처리 스레드 수
        private const int MaxConcurrentPlayers = 6000;
        public static long currentPacketIndex = 0;
        private static IntPtr iocpHandle;
        private static bool isRunning = true;
        private static Thread workerThread;
        private static System.Timers.Timer updateTimer;

        // 통계 및 성능 모니터링 변수들
        private static long totalBytesSent = 0;         // 총 전송 바이트
        private static long totalBytesReceived = 0;     // 총 수신 바이트
        private static long totalPacketsSent = 0;       // 총 전송 패킷 수
        private static long totalPacketsReceived = 0;   // 총 수신 패킷 수

        private static long lastBytesSent = 0;
        private static long lastBytesReceived = 0;
        private static System.Timers.Timer statsTimer;
        private static DateTime lastStatsTime;
        private static SpinLock packetStatsLock = new SpinLock();
        public class PacketStats
        {
            public long TotalProcessingTime = 0;
            public long PacketCount = 0;
        }

        public static Dictionary<PacketType, PacketStats> packetStats = new Dictionary<PacketType, PacketStats>();
        private static Dictionary<PacketType, Queue<KeyValuePair<DateTime, int>>> packetCounts = new Dictionary<PacketType, Queue<KeyValuePair<DateTime, int>>>();
        private static readonly TimeSpan statsWindow = TimeSpan.FromMinutes(5);

        // 스레드 신호
        public static ManualResetEvent allDone = new ManualResetEvent(false);   // false로 초기화 - 초기 상태가 "비신호" 상태
        private static PacketHandler packetHandler = new PacketHandler(UpdatePacketStats);

        /// <summary>
        /// 로컬 IP 주소를 가져오는 메서드
        /// </summary>
        public static string LocalIPAddress()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "172.31.1.176"; // 기본 IP 주소
        }

        public static void SaveConfig(string ip, int port, string configPath)
        {
            try
            {
                // 디렉토리가 존재하지 않으면 생성
                string directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (StreamWriter writer = new StreamWriter(configPath))
                {
                    writer.WriteLine($"ServerIP={ip}");
                    writer.WriteLine($"ServerPort={port}");
                }
                Console.WriteLine($"Config saved to: {configPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        /// <summary>
        /// 서버 시작 및 클라이언트 연결 대기 메서드
        /// </summary>
        private static void StartListening()
        {
            // 수신 데이터 버퍼
            byte[] bytes = new byte[1024];

            // 소켓의 로컬 엔드포인트 설정
            string serverIP = LocalIPAddress();
            int serverPort = 11000;
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            SaveConfig(serverIP, serverPort, "..\\..\\..\\..\\..\\ChatClient\\Assets\\Editor\\Debug\\config.txt");

            // TCP/IP 소켓 생성
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            /// <summary>
            /// 서버의 클라이언트 연결 수락 프로세스
            /// 비동기 방식으로 클라이언트의 연결 요청을 처리
            /// </summary>
            try
            {
                // 소켓을 로컬 엔드포인트에 바인딩
                // 특정 IP 주소와 포트 번호에 서버 소켓을 연결
                listener.Bind(localEndPoint);

                // 연결 대기열 설정 (MaxConcurrentPlayers / 4 의 대기 연결 허용)
                // 동시에 처리되지 않은 연결 요청을 큐에 저장
                listener.Listen(MaxConcurrentPlayers / 4);

                // 무한 루프로 계속해서 클라이언트 연결을 수락
                while (true)
                {
                    // ManualResetEvent를 비신호 상태로 설정
                    // 새로운 연결을 처리할 준비
                    allDone.Reset();

                    // 연결 대기 상태 콘솔에 출력
                    Console.WriteLine("\n연결을 기다리는 중...");
                    Console.WriteLine($"클라이언트는 다음 IP로 접속해야 합니다: {serverIP}:11000");

                    // 비동기적으로 클라이언트 연결 수락 시작
                    // AcceptCallback: 연결이 수락되었을 때 호출될 콜백 메서드
                    // listener: 콜백에 전달될 상태 객체
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                    // 연결이 수락될 때까지 현재 스레드 대기
                    // AcceptCallback에서 allDone.Set()이 호출되면 다음 진행
                    allDone.WaitOne();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버 시작 중 치명적 오류: {ex}");
                throw;
            }
            finally
            {
                listener.Close();
            }
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            ThreadMonitor.LogThreadInfo("AcceptCallback");
            try
            {
                // 클라이언트 소켓을 처리하는 소켓 가져오기
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                int clientId = SocketManager.AddClient(handler);

                // ClientConnection 객체 생성
                ClientConnection connection = new ClientConnection(handler);
                connection.ClientId = clientId;

                if (handler.Connected)
                {
                    handler.BeginReceive(connection.ReceiveBuffer, 0,
                        ClientConnection.ReceiveBufferSize, 0,
                        new AsyncCallback(ReadCallback), connection);
                }
                else
                {
                    Console.WriteLine("Connection lost. Attempting to reconnect...");
                    CleanupConnection(handler, connection);
                }
                Console.WriteLine($"New client connected. ID: {clientId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AcceptCallback: {ex.Message}");
            }
            finally
            {
                // 메인 스레드에 계속할 신호를 보냄
                allDone.Set();
            }
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            // 클라이언트로부터 데이터 수신
            ThreadMonitor.LogThreadInfo("ReadCallback");
            ClientConnection connection = (ClientConnection)ar.AsyncState;
            int threadId = Thread.CurrentThread.ManagedThreadId;
            Log.Instance.FileLog(Log.LogId.NET, Log.LogLevel.TRC_DATA, $"현재 스레드 ID: {threadId} ReadCallback");
            Socket handler = connection.Socket;

            try
            {
                // 데이터 수신 완료
                int bytesRead = handler.EndReceive(ar);
                Interlocked.Add(ref totalBytesReceived, bytesRead);

                // 수신된 바이트 > 0?
                if (bytesRead > 0)
                {
                    // 수신 데이터를 스트림에 추가
                    connection.AddData(connection.ReceiveBuffer, 0, bytesRead);

                    // 반복적으로 모든 완전한 패킷 처리
                    NetworkProcessor.ProcessAvailablePackets(connection, handler);

                    // 소켓 연결 상태 확인
                    if (handler.Connected)
                    {
                        // 다음 데이터 수신 대기
                        handler.BeginReceive(connection.ReceiveBuffer, 0,
                            ClientConnection.ReceiveBufferSize, 0,
                            new AsyncCallback(ReadCallback), connection);
                    }
                    else
                    {
                        // 연결 종료 처리
                        Console.WriteLine("Connection lost. Attempting to reconnect...");
                        CleanupConnection(handler, connection);
                    }
                }
                else
                {
                    // 연결 종료 처리
                    Console.WriteLine("Connection closed by client.");
                    CleanupConnection(handler, connection);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error: {e.Message}");
                // 연결 종료 처리
                CleanupConnection(handler, connection);
            }
        }

        private static void CleanupConnection(Socket handler, ClientConnection connection = null)
        {
            try
            {
                int clientId = SocketManager.GetClientId(handler);
                if (clientId != -1)
                {
                    PostRemovePcEvent(clientId);
                    SocketManager.RemoveClient(clientId);
                    Console.WriteLine($"Connection cleaned up for client ID: {clientId}");
                }

                handler.Close();  // 소켓 객체를 닫음

                // ClientConnection 사용
                connection?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during connection cleanup: {ex.Message}");
            }
        }

        private static void Send(Socket handler, string data)
        {
            // 문자열 데이터를 UTF8 인코딩을 사용하여 바이트 데이터로 변환
            byte[] byteData = Encoding.UTF8.GetBytes(data);

            // 원격 장치로 데이터를 전송 시작
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
        }
        public static void Send(Socket handler, byte[] byteData)
        {
            // 원격 장치로 데이터를 전송 시작
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
        }

        public static void SendPacket(Socket socket, PacketType packetType, byte[] data)
        {
            if (socket == null || !socket.Connected)
            {
                Console.WriteLine("Error sending packet: Socket is null or not connected.");
                int clientId = SocketManager.GetClientId(socket);
                if (clientId != -1)
                {
                    CleanupConnection(socket);
                }
                return;
            }

            try
            {
                // 패킷 헤더 생성
                ushort packetSize = (ushort)(NetBase.PacketHeader.HeaderSize + data.Length);
                PacketHeader header = new PacketHeader { Size = packetSize, Type = (ushort)packetType };
                byte[] headerBytes = header.ToBytes();

                // 패킷 헤더와 데이터 병합
                byte[] packet = new byte[packetSize];
                headerBytes.CopyTo(packet, 0);
                data.CopyTo(packet, NetBase.PacketHeader.HeaderSize);

                // 데이터 전송
                socket.BeginSend(packet, 0, packet.Length, SocketFlags.None, new AsyncCallback(SendCallback), socket);
                Interlocked.Add(ref totalBytesSent, packet.Length);
                Log.Instance.FileLog(Log.LogId.NET, Log.LogLevel.TRC_DATA, $"Send {packetType.ToString()} {packet.Length} bytes to server");
            }
            catch (ObjectDisposedException ex)
            {
                Console.WriteLine("Error sending packet: Socket has been disposed. {0}", ex.Message);
                int clientId = SocketManager.GetClientId(socket);
                if (clientId != -1)
                {
                    CleanupConnection(socket);
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Error sending packet: Socket exception. {0}", ex.Message);
                int clientId = SocketManager.GetClientId(socket);
                if (clientId != -1)
                {
                    CleanupConnection(socket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending packet: {0}", ex.Message);
                int clientId = SocketManager.GetClientId(socket);
                if (clientId != -1)
                {
                    CleanupConnection(socket);
                }
            }
        }


        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // 상태 객체에서 소켓을 가져오기
                Socket handler = ar.AsyncState as Socket;

                if (handler == null || !handler.Connected)
                {
                    Console.WriteLine("SendCallback: Socket is null or not connected.");
                    return;
                }

                // 원격 장치로 데이터 전송 완료
                int bytesSent = handler.EndSend(ar);
                Log.Instance.FileLog(Log.LogId.NET, Log.LogLevel.TRC_DATA, $"클라이언트로 {bytesSent} 바이트 전송.");
            }
            catch (ObjectDisposedException ex)
            {
                Console.WriteLine("SendCallback: Socket has been disposed. {0}", ex.Message);
            }
            catch (SocketException ex)
            {
                Console.WriteLine("SendCallback[SocketException] Error : {0} ", ex.Message.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendCallback[Exception] Error : {0} ", ex.Message.ToString());
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateIoCompletionPort(IntPtr FileHandle, IntPtr ExistingCompletionPort, IntPtr CompletionKey, uint NumberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool PostQueuedCompletionStatus(IntPtr CompletionPort, uint dwNumberOfBytesTransferred, IntPtr dwCompletionKey, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetQueuedCompletionStatus(IntPtr CompletionPort, out uint lpNumberOfBytesTransferred, out IntPtr lpCompletionKey, out IntPtr lpOverlapped, uint dwMilliseconds);

        private static void WorkerThreadFunction()
        {
            Console.WriteLine("Worker thread started.");
            while (isRunning)
            {
                uint bytesTransferred;
                IntPtr completionKey;
                IntPtr overlapped;
                if (GetQueuedCompletionStatus(iocpHandle, out bytesTransferred, out completionKey, out overlapped, uint.MaxValue))
                {
                    IOEventType eventType = (IOEventType)bytesTransferred;
                    ThreadMonitor.LogThreadInfo("WorkerThread");
                    switch (eventType)
                    {
                        case IOEventType.Update:
                            packetHandler.HandleUpdate();
                            break;

                        case IOEventType.ProcessPacket:
                            ProcessPacket(completionKey);
                            break;

                        case IOEventType.RemovePc:
                            HandleRemovePcEvent(overlapped);
                            break;

                        default:
                            Console.WriteLine($"Unknown event type: {eventType}");
                            break;
                    }
                }
            }
            Console.WriteLine("Worker thread stopped.");
        }

        public static void PostProcessPacketEvent(IntPtr packetInfoPtr)
        {
            PostQueuedCompletionStatus(iocpHandle, (uint)IOEventType.ProcessPacket, packetInfoPtr, IntPtr.Zero);
        }

        public static void PostUpdateEvent()
        {
            PostQueuedCompletionStatus(iocpHandle, (uint)IOEventType.Update, IntPtr.Zero, IntPtr.Zero);
        }

        private static void PostRemovePcEvent(int clientId)
        {
            RemovePcEventArgs eventArgs = new RemovePcEventArgs { ClientId = clientId };
            IntPtr eventArgsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(eventArgs));
            Marshal.StructureToPtr(eventArgs, eventArgsPtr, false);

            PostQueuedCompletionStatus(iocpHandle, (uint)IOEventType.RemovePc, IntPtr.Zero, eventArgsPtr);
        }

        private static void ProcessPacket(IntPtr completionKey)
        {
            PacketInfo packetInfo = (PacketInfo)Marshal.PtrToStructure(completionKey, typeof(PacketInfo));
            byte[] data = new byte[packetInfo.DataSize];
            Marshal.Copy(packetInfo.DataPtr, data, 0, packetInfo.DataSize);
            Log.Instance.FileLog(Log.LogId.PACKET, Log.LogLevel.TRC_DATA, $"Processing packet with index: {packetInfo.PacketIndex}");
            using (MemoryStream memoryStream = new MemoryStream(data))
            {
                packetHandler.HandlePacket(memoryStream, packetInfo.SocketHandle, packetInfo.ClientId, packetInfo.PacketIndex);
            }
            Marshal.FreeHGlobal(packetInfo.DataPtr);
            Marshal.FreeHGlobal(completionKey);
        }

        private static void HandleRemovePcEvent(IntPtr eventArgsPtr)
        {
            RemovePcEventArgs eventArgs = (RemovePcEventArgs)Marshal.PtrToStructure(eventArgsPtr, typeof(RemovePcEventArgs));
            //GameManager gameManager = GameManager.Instance;
            //Pc pc = gameManager.GetPcByClientId(eventArgs.ClientId);

            //if (pc != null)
            //{
            //    gameManager.DespawnCharacter(pc);
            //    gameManager.RemovePcByClientId(eventArgs.ClientId);
            //}
            Console.WriteLine($"PC removed for client ID: {eventArgs.ClientId}");
            Marshal.FreeHGlobal(eventArgsPtr);
        }

        private static void CleanUp()
        {
            isRunning = false;
            workerThread.Join();
            PostQueuedCompletionStatus(iocpHandle, 0, IntPtr.Zero, IntPtr.Zero);  // 워커 스레드 종료 신호

            if (iocpHandle != IntPtr.Zero)
            {
                CloseHandle(iocpHandle);
                Console.WriteLine("IOCP handle closed.");
            }

            updateTimer.Stop();
            updateTimer.Dispose();
        }

        static void Main(string[] args)
        {
            Log.Instance.InitLog();
            Log.Instance.LogSwitch(Log.LogId.SYSTEM, Log.LogLevel.TRC_DATA);
            Log.Instance.LogSwitch(Log.LogId.TIMER, Log.LogLevel.TRC_NOLOG);
            Log.Instance.LogSwitch(Log.LogId.NET, Log.LogLevel.TRC_NOLOG);
            Log.Instance.LogSwitch(Log.LogId.ITEM, Log.LogLevel.TRC_NOLOG);
            Log.Instance.LogSwitch(Log.LogId.SVO, Log.LogLevel.TRC_NOLOG);
            Log.Instance.LogSwitch(Log.LogId.PATH, Log.LogLevel.TRC_DATA);
            Log.Instance.LogSwitch(Log.LogId.PACKET, Log.LogLevel.TRC_NOLOG);
            Log.Instance.LogSwitch(Log.LogId.CHARACTER, Log.LogLevel.TRC_DATA);
            Log.Instance.LogSwitch(Log.LogId.NPC, Log.LogLevel.TRC_DATA);
            Log.Instance.LogSwitch(Log.LogId.SKILLBUFF, Log.LogLevel.TRC_DATA);
            Log.Instance.LogSwitch(Log.LogId.BROADCAST, Log.LogLevel.TRC_DATA);

            Log.Instance.FileLog(Log.LogId.SYSTEM, Log.LogLevel.TRC_DATA, "Log process run..");
            Log.Instance.FileLog(Log.LogId.SYSTEM, Log.LogLevel.TRC_DATA, "Process start..");

            ThreadMonitor.LogThreadInfo("Main Thread");

            // ThreadPool 설정
            ThreadPool.SetMinThreads(RECV_THREAD_COUNT, RECV_THREAD_COUNT);
            ThreadPool.SetMaxThreads(RECV_THREAD_COUNT, RECV_THREAD_COUNT);
            int workerThreads = 0;
            int completionPortThreads = 0;
            ThreadPool.GetMinThreads(out workerThreads, out completionPortThreads);
            Console.WriteLine($"Thread pool configured. Min worker threads: {workerThreads}, Min I/O threads: {completionPortThreads}");

            // IOCP 핸들만 생성 (-1은 INVALID_HANDLE_VALUE를 의미)
            iocpHandle = CreateIoCompletionPort(new IntPtr(-1), IntPtr.Zero, IntPtr.Zero, 0); // Updated to use new IntPtr(-1)
            if (iocpHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"Failed to create IOCP. Error: {error}");
                return;
            }
            Console.WriteLine("IOCP handle created successfully.");

            // 워커 스레드 시작
            workerThread = new Thread(WorkerThreadFunction);
            workerThread.Start();

            //GameManager gameManager = GameManager.Instance;


            // 주기적 업데이트 타이머 설정
            InitializeUpdateTimer();

            InitializeStatsTimer();
            StartListening();

            CleanUp();
        }

        private static void InitializeUpdateTimer()
        {
            updateTimer = new System.Timers.Timer(100); // 100ms마다 실행 (초당 30회)
            updateTimer.Elapsed += OnUpdateTimerElapsed;
            updateTimer.AutoReset = true;
            updateTimer.Enabled = true;
        }

        private static void OnUpdateTimerElapsed(object sender, ElapsedEventArgs e)
        {
            ThreadMonitor.LogThreadInfo("UpdateTimer");
            //ThreadMonitor.LogThreadInfo($"UpdateTimer Elapsed at {DateTime.Now.Ticks}");
            PostUpdateEvent();
        }

        private static void InitializeStatsTimer()
        {
            statsTimer = new System.Timers.Timer(30000); // 10초마다 실행
            statsTimer.Elapsed += OnStatsTimerElapsed;
            statsTimer.AutoReset = true;
            statsTimer.Enabled = true;
            lastStatsTime = DateTime.Now;
        }

        private static void OnStatsTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ThreadMonitor.LogThreadInfo("StatsTimer");
            DateTime now = DateTime.Now;
            double elapsedSeconds = (now - lastStatsTime).TotalSeconds;

            long bytesSentSinceLastCheck = totalBytesSent - lastBytesSent;
            long bytesReceivedSinceLastCheck = totalBytesReceived - lastBytesReceived;

            double bytesSentPerSecond = bytesSentSinceLastCheck / elapsedSeconds;
            double bytesReceivedPerSecond = bytesReceivedSinceLastCheck / elapsedSeconds;

            Console.WriteLine($"[{now}] 통계:");
            Console.WriteLine($"  누적 전송 패킷: {totalPacketsSent}, 누적 수신 패킷: {totalPacketsReceived}");
            Console.WriteLine($"  누적 전송 바이트: {totalBytesSent}, 누적 수신 바이트: {totalBytesReceived}");
            Console.WriteLine($"  초당 전송 바이트: {bytesSentPerSecond:F2} B/s, 초당 수신 바이트: {bytesReceivedPerSecond:F2} B/s");
            Console.WriteLine("패킷 처리 시간 통계:");
            try
            {
                packetStatsLock.Lock();
                foreach (var stat in packetStats)
                {
                    if (stat.Value.PacketCount > 0)
                    {
                        double averageProcessingTime = (double)stat.Value.TotalProcessingTime / stat.Value.PacketCount;
                        Console.WriteLine($"  {stat.Key}: 평균 {averageProcessingTime:F2}ms (총 {stat.Value.PacketCount}개)");
                    }
                }
            }
            finally
            {
                packetStatsLock.Unlock();
            }
            Console.WriteLine($"[{now}] 통계:");
            Console.WriteLine($"  누적 전송 패킷: {totalPacketsSent}, 누적 수신 패킷: {totalPacketsReceived}");
            Console.WriteLine($"  누적 전송 바이트: {totalBytesSent}, 누적 수신 바이트: {totalBytesReceived}");
            Console.WriteLine($"  초당 전송 바이트: {bytesSentPerSecond:F2} B/s, 초당 수신 바이트: {bytesReceivedPerSecond:F2} B/s");

            Console.WriteLine("패킷 처리 시간 통계:");
            try
            {
                packetStatsLock.Lock();
                foreach (var stat in packetStats)
                {
                    if (stat.Value.PacketCount > 0)
                    {
                        double averageProcessingTime = (double)stat.Value.TotalProcessingTime / stat.Value.PacketCount;
                        Console.WriteLine($"  {stat.Key}: 평균 {averageProcessingTime:F2}ms (총 {stat.Value.PacketCount}개)");
                    }
                }
            }
            finally
            {
                packetStatsLock.Unlock();
            }

            lastBytesSent = totalBytesSent;
            lastBytesReceived = totalBytesReceived;
            lastStatsTime = now;

            // 최근 5분 동안의 각 패킷의 초당 Recv 카운트 계산 및 출력
            Console.WriteLine("최근 5분 동안의 각 패킷의 초당 Recv 카운트:");

            try
            {
                packetStatsLock.Lock();
                foreach (var stat in packetStats)
                {
                    PacketType packetType = stat.Key;
                    int newCount = (int)stat.Value.PacketCount;

                    if (!packetCounts.ContainsKey(packetType))
                    {
                        packetCounts[packetType] = new Queue<KeyValuePair<DateTime, int>>();
                    }

                    packetCounts[packetType].Enqueue(new KeyValuePair<DateTime, int>(now, newCount));

                    // 5분이 지난 데이터 제거
                    while (packetCounts[packetType].Count > 0 && now - packetCounts[packetType].Peek().Key > statsWindow)
                    {
                        packetCounts[packetType].Dequeue();
                    }

                    if (packetCounts[packetType].Count > 1)
                    {
                        var oldest = packetCounts[packetType].Peek();
                        int countDiff = newCount - oldest.Value;
                        double timeDiff = (now - oldest.Key).TotalSeconds;
                        double packetPerSecond = countDiff / timeDiff;

                        Console.WriteLine($"  {packetType}: {packetPerSecond:F2} packets/s");
                    }
                    else
                    {
                        Console.WriteLine($"  {packetType}: 충분한 데이터가 없습니다.");
                    }
                }
            }
            finally
            {
                packetStatsLock.Unlock();
            }
            lastBytesSent = totalBytesSent;
            lastBytesReceived = totalBytesReceived;
            lastStatsTime = now;

            ThreadMonitorExtensions.SaveThreadInfoToFile();
        }
        public static void UpdatePacketStats(PacketType packetType, long processingTime)
        {
            try
            {
                packetStatsLock.Lock();
                if (!packetStats.ContainsKey(packetType))
                {
                    packetStats[packetType] = new PacketStats();
                }

                packetStats[packetType].TotalProcessingTime += processingTime;
                packetStats[packetType].PacketCount++;
            }
            finally
            {
                packetStatsLock.Unlock();
            }
        }
    }
}