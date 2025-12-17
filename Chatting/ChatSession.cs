using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace dn42Bot.Chatting;

internal static class ChatSession
{
    static AsyncLock _lock = new();
    readonly static Dictionary<long, ChatRoom> _chatRooms = new();

    public static ChatRoom GetOrCreateChatRoom(long chatId)
    {
        using (_lock.Lock())
        {
            ref var chatRoom = ref CollectionsMarshal.GetValueRefOrAddDefault(_chatRooms, chatId, out var isExists);
            if (!isExists)
            {
                chatRoom = new ChatRoom()
                {
                    Id = chatId
                };
            }
            return chatRoom!;
        }
    }
    public static async Task<ChatRoom> GetOrCreateChatRoomAsync(long chatId)
    {
        using (await _lock.LockAsync())
        {
            ref var chatRoom = ref CollectionsMarshal.GetValueRefOrAddDefault(_chatRooms, chatId, out var isExists);
            if (!isExists)
            {
                chatRoom = new ChatRoom()
                {
                    Id = chatId
                };
            }
            return chatRoom!;
        }
    }
    public static async ValueTask CleanUpAsync()
    {
        if(_chatRooms.Count == 0)
        {
            return;
        }
        using (await _lock.LockAsync())
        {
            if (_chatRooms.Count == 0)
            {
                return;
            }
            await Parallel.ForEachAsync(_chatRooms.Values, (i, token) =>
            {
                return i.CleanUpAsync();
            });
        }
    }
}
