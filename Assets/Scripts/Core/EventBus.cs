using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;

// 서로 모르는 시스템끼리 이벤트를 주고받는 전역 이벤트 버스. 이벤트 타입(T)마다 구독자 목록이 따로 있다.
// 같은 오브젝트 안의 통신은 C# event나 직접 참조를 쓰고, 시스템 간 통신(예: 보스 처치 → 룬 선택)에만 사용한다.
// 이벤트는 GC 할당이 없도록 struct로 정의한다.
// 구독자는 OnEnable에서 Subscribe, OnDisable에서 Unsubscribe 한다. 해제하지 않으면 파괴된 오브젝트가 계속 호출된다.
public static class EventBus<T> where T : struct
{
    private static readonly List<Action<T>> _handlers = new List<Action<T>>();

    public static void Subscribe(Action<T> handler)
    {
        if (handler == null || _handlers.Contains(handler))
        {
            return;
        }

        _handlers.Add(handler);
    }

    public static void Unsubscribe(Action<T> handler)
    {
        _handlers.Remove(handler);
    }

    // 구독 순서대로 호출한다.
    // 핸들러 안에서 구독/해제하거나 다른 이벤트를 연쇄 발행해도 안전하도록, 발행 시점의 목록을 풀링된 배열에 복사해 순회한다.
    public static void Publish(T eventData)
    {
        int count = _handlers.Count;

        if (count == 0)
        {
            return;
        }

        Action<T>[] snapshot = ArrayPool<Action<T>>.Shared.Rent(count);
        _handlers.CopyTo(snapshot);

        try
        {
            for (int i = 0; i < count; i++)
            {
                // 한 핸들러의 예외 때문에 나머지 구독자가 호출되지 않는 일이 없도록 한다.
                try
                {
                    snapshot[i](eventData);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
        finally
        {
            ArrayPool<Action<T>>.Shared.Return(snapshot, true);
        }
    }

    public static void Clear()
    {
        _handlers.Clear();
    }
}
