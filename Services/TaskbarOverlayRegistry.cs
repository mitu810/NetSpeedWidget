using System;
using System.Collections.Generic;

namespace NetSpeedWidget.Services;

/// <summary>按真实任务栏句柄保存窗口，屏幕排序变化不会重新分配已有窗口。</summary>
public sealed class TaskbarOverlayRegistry<T> : IDisposable where T : class, IDisposable
{
    private readonly Dictionary<IntPtr, T> _windows = new();
    private readonly Func<IntPtr, T> _create;

    public TaskbarOverlayRegistry(Func<IntPtr, T> create) => _create = create;

    /// <summary>同步当前任务栏集合，仅创建新项并释放消失项。</summary>
    public IReadOnlyList<T> Synchronize(IReadOnlyList<IntPtr> taskbars)
    {
        // 1. 保留现存窗口，按调用方的当前顺序返回对应窗口。
        var active = new HashSet<IntPtr>(taskbars);
        var result = new List<T>(taskbars.Count);
        foreach (var handle in taskbars)
        {
            if (!_windows.TryGetValue(handle, out var window))
                _windows.Add(handle, window = _create(handle));
            result.Add(window);
        }
        // 2. 仅释放已经消失的任务栏窗口。
        foreach (var handle in new List<IntPtr>(_windows.Keys))
        {
            if (active.Contains(handle)) continue;
            _windows[handle].Dispose();
            _windows.Remove(handle);
        }
        return result;
    }

    /// <summary>重新创建一份已损坏的窗口，不影响其他屏幕。</summary>
    public T Recreate(IntPtr handle)
    {
        if (_windows.Remove(handle, out var window)) window.Dispose();
        return _windows[handle] = _create(handle);
    }

    /// <summary>释放所有管理的窗口。</summary>
    public void Dispose()
    {
        foreach (var window in _windows.Values) window.Dispose();
        _windows.Clear();
    }
}
