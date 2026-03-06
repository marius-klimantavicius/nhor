using System;
using System.Collections.Generic;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor;

public class EventManager
{
    private readonly Dictionary<string, EventRegistration> _configuredEvents = new();

    public void ConfigureEvent(string eventName, Action<ulong> setId, Action<ulong> clearId)
    {
        _configuredEvents[eventName] = new EventRegistration(eventName, setId, clearId);
    }

    public bool TryRegisterEvent(NativeComponentRenderer renderer, string eventName, ulong eventHandlerId)
    {
        if (_configuredEvents.TryGetValue(eventName, out var eventRegistration))
        {
            renderer.RegisterEvent(eventHandlerId, eventRegistration.ClearId);
            eventRegistration.SetId(eventHandlerId);
            return true;
        }
        return false;
    }
}

internal class EventRegistration
{
    public EventRegistration(string eventName, Action<ulong> setId, Action<ulong> clearId)
    {
        EventName = eventName;
        SetId = setId;
        ClearId = clearId;
    }

    public string EventName { get; }
    public Action<ulong> SetId { get; }
    public Action<ulong> ClearId { get; }
}
