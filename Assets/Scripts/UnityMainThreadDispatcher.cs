using System;
using System.Collections.Concurrent;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();
    private static UnityMainThreadDispatcher _instance;

    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            var go = new GameObject("UnityMainThreadDispatcher");
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
        return _instance;
    }

    public void Enqueue(Action action)
    {
        Debug.Log($"[{nameof(UnityMainThreadDispatcher)}] Enqueueing: '{nameof(action)}'");
        _executionQueue.Enqueue(action);
    }

    private void Update()
    {
        while (_executionQueue.TryDequeue(out var action))
        {
            action();
        }
    }
}