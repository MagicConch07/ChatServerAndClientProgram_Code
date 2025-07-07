using System;
using System.IO;

/// <summary>
/// ObjectPool에서 사용할 수 있는 MemoryStream 래퍼 클래스 (IPoolable 버전)
/// </summary>
public class PoolableMemoryStream : IPoolable, IDisposable
{
    private MemoryStream _stream;
    private bool _disposed = false;

    public PoolableMemoryStream()
    {
        _stream = new MemoryStream();
    }

    /// <summary>
    /// 내부 MemoryStream에 접근하는 프로퍼티
    /// </summary>
    public MemoryStream Stream => _stream;

    /// <summary>
    /// IPoolable 인터페이스 구현
    /// 풀로 반환되기 전에 호출되는 리셋 메서드
    /// </summary>
    public void Reset()
    {
        if (_stream != null)
        {
            _stream.SetLength(0);    // 길이를 0으로 설정
            _stream.Position = 0;    // 위치를 처음으로 이동
        }
    }

    /// <summary>
    /// byte[] 데이터로 스트림을 초기화하는 메서드
    /// HandlePacket에서 bodyData로 스트림을 만들 때 사용
    /// </summary>
    public void SetData(byte[] data)
    {
        _stream.SetLength(0);
        _stream.Position = 0;
        if (data != null && data.Length > 0)
        {
            _stream.Write(data, 0, data.Length);
            _stream.Position = 0;  // 읽기를 위해 처음으로 이동
        }
    }

    /// <summary>
    /// IDisposable 구현
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _stream?.Dispose();
                _stream = null;
            }
            _disposed = true;
        }
    }

    ~PoolableMemoryStream()
    {
        Dispose(false);
    }
}