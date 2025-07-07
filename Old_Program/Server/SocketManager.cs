using System;
using System.Collections.Generic;
using System.Net.Sockets;
using ChatServer;

public class SocketManager
{
    private static readonly SpinLock _spinLock = new SpinLock();
    private static readonly Dictionary<int, Socket> _connectedClients = new Dictionary<int, Socket>();
    private static int _nextClientId = 1;

    public static int AddClient(Socket clientSocket)
    {
        int clientId;
        try
        {
            _spinLock.Lock();
            clientId = _nextClientId++;
            _connectedClients[clientId] = clientSocket;
            return clientId;
        }
        finally
        {
            _spinLock.Unlock();
        }
    }

    public static void RemoveClient(int clientId)
    {
        try
        {
            _spinLock.Lock();
            if (_connectedClients.ContainsKey(clientId))
            {
                _connectedClients.Remove(clientId);
            }
        }
        finally
        {
            _spinLock.Unlock();
        }
    }

    public static void BroadcastToAll(PacketType type, byte[] data, int excludeClientId = -1)
    {
#if TimeLogger
        var timer = TimeLogger.Instance;
        timer.Start(TimeLogger.TimerId.BroadcastToAll);
#endif
        List<KeyValuePair<int, Socket>> clientsSnapshot;
        List<int> clientsToRemove = new List<int>();

        // 스냅샷 생성
        try
        {
            _spinLock.Lock();
            clientsSnapshot = new List<KeyValuePair<int, Socket>>(_connectedClients);
        }
        finally
        {
            _spinLock.Unlock();
        }

        // 브로드캐스트 수행 (락 외부에서 실행하여 성능 향상)
        foreach (var client in clientsSnapshot)
        {
            if (client.Key != excludeClientId)
            {
                try
                {
                    Program.SendPacket(client.Value, type, data);
                    Log.Instance.FileLog(Log.LogId.NET, Log.LogLevel.TRC_DATA, $"Sent {type} packet to client {client.Key}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending packet to client {client.Key}: {ex.Message}");
                    clientsToRemove.Add(client.Key);
                }
            }
        }

        // 연결이 끊긴 클라이언트 제거
        if (clientsToRemove.Count > 0)
        {
            try
            {
                _spinLock.Lock();
                foreach (var clientId in clientsToRemove)
                {
                    if (_connectedClients.ContainsKey(clientId))
                    {
                        _connectedClients.Remove(clientId);
                        Console.WriteLine($"Removed client {clientId} due to disconnected socket.");
                    }
                }
            }
            finally
            {
                _spinLock.Unlock();
            }
        }

#if TimeLogger
        timer.Stop(TimeLogger.TimerId.BroadcastToAll);
#endif
    }

    public static int GetClientId(Socket clientSocket)
    {
        try
        {
            _spinLock.Lock();
            foreach (var client in _connectedClients)
            {
                if (client.Value == clientSocket)
                {
                    return client.Key;
                }
            }
            return -1;
        }
        finally
        {
            _spinLock.Unlock();
        }
    }

    public static Socket GetSocket(int clientId)
    {
        try
        {
            _spinLock.Lock();
            return _connectedClients.TryGetValue(clientId, out Socket socket) ? socket : null;
        }
        finally
        {
            _spinLock.Unlock();
        }
    }
}