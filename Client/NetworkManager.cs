using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Linq;
using System.Threading;
using BotClient;
using MKChatServer;
using MKDir;

public class NetworkManager : MonoSingleton<NetworkManager>
{
    private Socket _socket;
    private bool _isConnected = false;
    private bool _isConnecting = false;
    private List<byte> _receivedDataBuffer = new List<byte>();
    private PacketHandler _packetHandler;
    private Thread _networkThread;
    private bool _running = true;

    [Header("Connection Settings")]
    public bool exitOnConnectionFailure = true;

    [Header("Server Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 11000;

    // Unity 메인 스레드에서 실행할 액션을 큐에 넣기 위한 코드
    private Queue<Action> _mainThreadActions = new Queue<Action>();
    private readonly object _queueLock = new object(); // 스레드 안전성을 위한 락

    protected override void Awake()
    {
        base.Awake();

        InitializeManagers();
    }

    private void Update()
    {
        lock (_queueLock)
        {
            while (_mainThreadActions.Count > 0)
            {
                try
                {
                    _mainThreadActions.Dequeue().Invoke();
                }
                catch (Exception e)
                {
#if UNITY_EDITOR
                    Debug.LogError($"[NETWORK] Error executing queued action: {e.Message}");
#endif
                }
            }
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    private void InitializeManagers()
    {
        _packetHandler = new PacketHandler();
    }

    public void ConnectToServer()
    {
        if (_isConnected || _isConnecting)
            return;

        _isConnecting = true;
#if UNITY_EDITOR
        Debug.Log($"[NETWORK] Connecting to server {serverIP}:{serverPort}");
#endif

        try
        {
            IPAddress ipAddress = IPAddress.Parse(serverIP);
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, serverPort);

            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(remoteEP);
            _socket.NoDelay = true;
            _socket.Blocking = false;

#if UNITY_EDITOR
            Debug.Log($"[NETWORK] Connected to {_socket.RemoteEndPoint.ToString()}");
#endif

            _isConnected = true;
            _isConnecting = false;
            _running = true;
            _networkThread = new Thread(NetworkThreadMethod);
            _networkThread.IsBackground = true;
            _networkThread.Start();
        }
        catch (Exception e)
        {
#if UNITY_EDITOR
            string errorMessage = $"[NETWORK] Connection failed: {e.Message}";
            Debug.LogError(errorMessage);
#endif
            _isConnected = false;
            _isConnecting = false;

            // 연결 실패 시 프로그램 종료
            if (exitOnConnectionFailure)
            {
                QueueOnMainThread(() =>
                {
#if UNITY_EDITOR
                    Debug.LogError("[NETWORK] Exiting application due to connection failure...");
#endif
                    Application.Quit();

                    // 에디터에서 실행 중일 경우를 위한 처리
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                });
            }
        }
    }

    public void Disconnect()
    {
        _running = false;

        if (_networkThread != null && _networkThread.IsAlive)
        {
            _networkThread.Join(1000);
        }

        if (_socket != null && _isConnected)
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
            catch (Exception e)
            {
#if UNITY_EDITOR
                Debug.LogError($"[NETWORK] Error during disconnect: {e.Message}");
#endif
            }
        }

        _isConnected = false;
    }

    public void QueueOnMainThread(Action action)
    {
        if (action == null)
            return;

        lock (_queueLock)
        {
            _mainThreadActions.Enqueue(action);
        }
    }

    private void NetworkThreadMethod()
    {
        byte[] buffer = new byte[1024 * 16];

        while (_running)
        {
            if (!_isConnected)
            {
                Thread.Sleep(100);
                continue;
            }

            try
            {
                bool dataReceived = false;
                do
                {
                    if (_socket.Poll(0, SelectMode.SelectRead))
                    {
                        int bytesReceived = _socket.Receive(buffer);
                        if (bytesReceived > 0)
                        {
                            ProcessReceivedData(_socket, buffer, bytesReceived);
                            dataReceived = true;
                        }
                        else
                        {
                            _isConnected = false;
#if UNITY_EDITOR
                            Debug.LogError("[NETWORK] Connection lost");
#endif

                            // 연결 끊김 시 프로그램 종료
                            if (exitOnConnectionFailure)
                            {
                                QueueOnMainThread(() =>
                                {
#if UNITY_EDITOR
                                    Debug.LogError("[NETWORK] Exiting application due to connection loss...");
#endif
                                    Application.Quit();

                                    // 에디터에서 실행 중일 경우를 위한 처리
#if UNITY_EDITOR
                                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                                });
                            }
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
                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
#if UNITY_EDITOR
                string errorMessage = $"[NETWORK] Error in network thread: {e.Message}";
                Console.WriteLine(errorMessage);
#endif

                _isConnected = false;

                // 네트워크 스레드에서 오류 발생 시 프로그램 종료
                if (exitOnConnectionFailure)
                {
                    QueueOnMainThread(() =>
                    {
#if UNITY_EDITOR
                        Debug.LogError("[NETWORK] Exiting application due to network thread error...");
#endif

                        Application.Quit();

                        // 에디터에서 실행 중일 경우를 위한 처리
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#endif
                    });
                }
                break;
            }
        }
    }

    private void ProcessReceivedData(Socket socket, byte[] receivedData, int bytesReceived)
    {
        // 스레드 안전한 데이터 처리
        lock (_receivedDataBuffer)
        {
            _receivedDataBuffer.AddRange(receivedData.Take(bytesReceived));
        }

        // 패킷 처리는 메인 스레드에서 수행
        QueueOnMainThread(() => ProcessPackets(socket));
    }

    private void ProcessPackets(Socket socket)
    {
        lock (_receivedDataBuffer)
        {
            while (_receivedDataBuffer.Count >= NetBase.PacketHeader.HeaderSize)
            {
                byte[] headerBytes = _receivedDataBuffer.Take(NetBase.PacketHeader.HeaderSize).ToArray();
                NetBase.PacketHeader receivedHeader = NetBase.PacketHeader.FromBytes(headerBytes);

                if (_receivedDataBuffer.Count >= receivedHeader.Size)
                {
                    byte[] packetData = _receivedDataBuffer.Take(receivedHeader.Size).ToArray();

                    using (MemoryStream packetStream = new MemoryStream(packetData))
                    {
                        _packetHandler.HandlePacket(packetStream, socket);
                    }

                    _receivedDataBuffer.RemoveRange(0, receivedHeader.Size);
                }
                else
                {
                    break;
                }
            }
        }
    }

    private void SendPacket(PacketType packetType, byte[] data)
    {
        if (!_isConnected || _socket == null)
            return;

        try
        {
            ushort packetSize = (ushort)(NetBase.PacketHeader.HeaderSize + data.Length);
            NetBase.PacketHeader header = new NetBase.PacketHeader { Size = packetSize, Type = (ushort)packetType };
            byte[] headerBytes = header.ToBytes();

            byte[] packet = new byte[packetSize];
            headerBytes.CopyTo(packet, 0);
            data.CopyTo(packet, NetBase.PacketHeader.HeaderSize);

            int bytesSend = _socket.Send(packet);
#if UNITY_EDITOR
            Debug.Log($"[NETWORK] Sent {bytesSend} bytes to server, PacketType: {packetType.ToString()}");
#endif
        }
        catch (Exception ex)
        {
#if UNITY_EDITOR
            string errorMessage = $"[NETWORK] Error sending packet: {ex.Message}";
            Debug.LogError(errorMessage);
#endif

            _isConnected = false;

            // 패킷 전송 오류 시 프로그램 종료
            if (exitOnConnectionFailure)
            {
                QueueOnMainThread(() =>
                {
#if UNITY_EDITOR
                    Debug.LogError("[NETWORK] Exiting application due to packet sending failure...");
#endif

                    Application.Quit();

                    // 에디터에서 실행 중일 경우를 위한 처리
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                });
            }
        }
    }
}
