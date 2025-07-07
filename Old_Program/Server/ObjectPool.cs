using System;
using System.Collections.Generic;

/// <summary>
/// 풀링 가능한 객체가 구현해야 하는 인터페이스
/// </summary>
public interface IPoolable
{
    void Reset();
}

/// <summary>
/// 객체 풀 클래스 (CObject 의존성 제거 버전)
/// </summary>
/// <typeparam name="T">풀링할 객체 타입</typeparam>
public class ObjectPool<T> where T : class, IPoolable, new()
{
    private Queue<T> pool = new Queue<T>();
    private int maxSize;
    private int growSize;

    public ObjectPool(int initialSize, int maxSize, int growSize)
    {
        this.maxSize = maxSize;
        this.growSize = growSize;

        // 초기 객체들을 생성하여 풀에 추가
        for (int i = 0; i < initialSize; i++)
        {
            pool.Enqueue(new T());
        }
    }

    public T Get()
    {
        // 풀이 비어있으면 확장
        if (pool.Count == 0)
        {
            GrowPool();
        }
        // 큐에서 객체를 꺼내서 반환
        return pool.Dequeue();
    }

    public void Return(T obj)
    {
        if (obj == null)
            return;

        // 객체를 초기 상태로 리셋
        obj.Reset();

        // 풀의 크기가 최대 크기보다 작은 경우에만 객체를 다시 풀에 추가
        if (pool.Count < maxSize)
        {
            pool.Enqueue(obj);
        }
        // maxSize를 초과하면 객체를 버림 (GC가 처리)
    }

    private void GrowPool()
    {
        // 실제로 생성할 객체 수 계산 (maxSize를 초과하지 않도록)
        int growCount = Math.Min(growSize, maxSize - pool.Count);

        // 새로운 객체들을 생성하여 풀에 추가
        for (int i = 0; i < growCount; i++)
        {
            pool.Enqueue(new T());
        }

        // 디버깅을 위한 로그 출력
        Console.WriteLine($"Pool for {typeof(T).Name} grew by {growCount}. New size: {pool.Count}");
    }

    public int Count => pool.Count;
}