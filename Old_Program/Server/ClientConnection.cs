using System;
using System.IO;
using System.Net.Sockets;

/// <summary>
/// 클라이언트 연결 상태를 관리하는 클래스
/// 소켓 통신 및 데이터 버퍼링을 담당
/// </summary>
public class ClientConnection : IDisposable
{
    // 락 객체 및 상태 변수
    public readonly object _lock = new object();
    private bool _disposed = false;

    // 소켓 및 식별 정보
    public Socket Socket { get; set; }
    public int ClientId { get; set; } = -1;

    // 수신 버퍼 관련 상수 및 변수
    public const int ReceiveBufferSize = 8 * 1024;  // 8KB
    public byte[] ReceiveBuffer { get; } = new byte[ReceiveBufferSize];

    // 누적 데이터를 저장하는 메모리 스트림
    public MemoryStream _dataBuffer = new MemoryStream();

    /// <summary>
    /// 생성자 - 소켓으로 클라이언트 연결 초기화
    /// </summary>
    public ClientConnection(Socket socket)
    {
        Socket = socket ?? throw new ArgumentNullException(nameof(socket));
    }

    /// <summary>
    /// 새 데이터를 스레드 안전하게 버퍼에 추가
    /// </summary>
    public void AddData(byte[] data, int offset, int count)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (count <= 0)
            return;

        lock (_lock)
        {
            _dataBuffer.Write(data, offset, count);
        }
    }

    // DataStream 속성 - 기존 코드와의 호환성 유지
    public MemoryStream DataStream
    {
        get
        {
            lock (_lock)
            {
                return _dataBuffer;
            }
        }
    }

    /// <summary>
    /// 리소스 해제
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 리소스 해제 구현
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 관리 리소스 해제
                    _dataBuffer?.Dispose();
                    _dataBuffer = null;
                }
                _disposed = true;
            }
        }
    }

    ~ClientConnection()
    {
        Dispose(false);
    }
}
