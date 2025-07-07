// 싱글톤 기본 클래스
public abstract class Singleton<T> where T : class, new()
{
    private static readonly object _lock = new object();
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                    }
                }
            }
            return _instance;
        }
    }
}

