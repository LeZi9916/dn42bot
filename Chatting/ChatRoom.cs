using dn42Bot.Buffers;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot.Types;

namespace dn42Bot.Chatting;

internal class ChatRoom
{
    public required long Id { get; init; }

    AsyncLock _lock = new();
    readonly HashSet<IController> _requests = new(16);
    readonly Dictionary<int, ICallbackController> _registedCallback = new(16);
    readonly Dictionary<ICallbackController, int> _rRegistedCallback = new(16);

    public void AddSession(IController session)
    {
        using (_lock.Lock())
        {
            _requests.Add(session);
        }
    }
    public async Task AddSessionAsync(IController session)
    {
        using (await _lock.LockAsync())
        {
            _requests.Add(session);
        }
    }
    public void RegisterCallback(int messageId, ICallbackController controller)
    {
        using (_lock.Lock())
        {
            if(!_requests.Contains(controller))
            {
                throw new InvalidOperationException("");
            }
            _registedCallback[messageId] = controller;
        }
    }
    public void UnregisterCallback(int messageId)
    {
        using (_lock.Lock())
        {
            _registedCallback.Remove(messageId);
        }
    }
    public async Task RegisterCallbackAsync(int messageId, ICallbackController controller)
    {
        using (await _lock.LockAsync())
        {
            if (!_requests.Contains(controller))
            {
                throw new InvalidOperationException("");
            }
            _registedCallback[messageId] = controller;
            _rRegistedCallback[controller] = messageId;
        }
    }
    public async Task UnregisterCallbackAsync(int messageId)
    {
        using (await _lock.LockAsync())
        {
            if(_registedCallback.Remove(messageId, out var controller))
            {
                _rRegistedCallback.Remove(controller);
            }            
        }
    }
    public void OnCallback(CallbackQuery callbackQuery)
    {
        OnCallbackAsync(callbackQuery).Wait();
    }
    public async Task OnCallbackAsync(CallbackQuery callbackQuery)
    {
        ArgumentNullException.ThrowIfNull(callbackQuery, nameof(callbackQuery));
        var originMessageId = callbackQuery.Message?.Id;
        if (originMessageId is null || !_registedCallback.TryGetValue((int)originMessageId, out var callbackHandler))
        {
            BotLogger.LogWarning($"U: {callbackQuery.From.Id}|C: {Id}|Received an unregistered callback query");
            return;
        }
        await callbackHandler.OnCallbackAsync(callbackQuery);
    }
    public async ValueTask CleanUpAsync()
    {
        if (_requests.Count == 0 && _registedCallback.Count == 0 && _rRegistedCallback.Count == 0)
        {
            return; 
        }
        else if(_requests.Count == 1)
        {
            return;
        }
        using var buffer = new RentedList<IController>();
        using (await _lock.LockAsync())
        {
            if (_requests.Count == 0 && _registedCallback.Count == 0 && _rRegistedCallback.Count == 0)
            {
                return;
            }
            else if (_requests.Count == 1)
            {
                return;
            }
            else if (_requests.Count == 0)
            {
                _registedCallback.Clear();
                _rRegistedCallback.Clear();
                return;
            }

            foreach (var request in _requests.OrderBy(x => x.CreateAt).SkipLast(1))
            {
                switch (request.State)
                {
                    case ControllerStatus.Running:
                        continue;
                    case ControllerStatus.StandBy:
                    case ControllerStatus.Completed:
                        buffer.Add(request);
                        if(request is ICallbackController callbackRequest)
                        {
                            _rRegistedCallback.Remove(callbackRequest, out var msgId);
                            _registedCallback.Remove(msgId);
                        }
                        break;
                }
            }
            foreach(var request in buffer)
            {
                _requests.Remove(request);
            }
        }

        await Task.Yield();
        await Parallel.ForEachAsync(buffer, (i, token) =>
        {
            return i.DisposeAsync();
        });
    }
}
