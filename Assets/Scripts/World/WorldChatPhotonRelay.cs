using System;
using Fusion;
using MysticJourney.API.Models.Response;
using UnityEngine;

public class WorldChatPhotonRelay : NetworkBehaviour
{
    public static WorldChatPhotonRelay Instance { get; private set; }

    public bool IsReady => Runner != null && Runner.IsRunning;

    public event Action<WorldChatMessageResponse> WorldMessageReceived;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WorldChatPhotonRelay] Duplicate relay found. Keeping the first instance.");
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool BroadcastWorldMessage(WorldChatMessageResponse message)
    {
        if (message == null || message.ChatMessageId <= 0)
        {
            return false;
        }

        if (!IsReady)
        {
            return false;
        }

        NetworkString<_128> senderName = TrimForFusion(message.SenderName, 120);
        NetworkString<_512> senderAvatarUrl = TrimForFusion(message.SenderAvatarUrl, 500);
        NetworkString<_512> content = TrimForFusion(message.Content, 500);
        NetworkString<_64> sentAt = TrimForFusion(message.SentAt, 60);

        RPC_WorldMessageReceived(
            message.ChatMessageId,
            message.SenderId,
            senderName,
            senderAvatarUrl,
            content,
            message.IsReported,
            message.IsHidden,
            sentAt);

        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_WorldMessageReceived(
        int chatMessageId,
        int senderId,
        NetworkString<_128> senderName,
        NetworkString<_512> senderAvatarUrl,
        NetworkString<_512> content,
        bool isReported,
        bool isHidden,
        NetworkString<_64> sentAt)
    {
        WorldMessageReceived?.Invoke(new WorldChatMessageResponse
        {
            ChatMessageId = chatMessageId,
            SenderId = senderId,
            SenderName = senderName.ToString(),
            SenderAvatarUrl = senderAvatarUrl.ToString(),
            Channel = "World",
            Content = content.ToString(),
            IsReported = isReported,
            IsHidden = isHidden,
            SentAt = sentAt.ToString()
        });
    }

    private static string TrimForFusion(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
