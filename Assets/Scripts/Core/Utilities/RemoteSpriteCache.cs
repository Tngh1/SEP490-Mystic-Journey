using System;
using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using UnityEngine;
using UnityEngine.Networking;

public static class RemoteSpriteCache
{
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, List<Action<Sprite>>> Pending = new Dictionary<string, List<Action<Sprite>>>();

    public static Sprite GetCached(string url)
    {
        var normalizedUrl = NormalizeUrl(url);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
            return null;

        return Sprites.TryGetValue(normalizedUrl, out var sprite) ? sprite : null;
    }

    public static void Load(MonoBehaviour runner, string url, Action<Sprite> callback)
    {
        var normalizedUrl = NormalizeUrl(url);
        if (string.IsNullOrWhiteSpace(normalizedUrl) || runner == null)
        {
            callback?.Invoke(null);
            return;
        }

        if (Sprites.TryGetValue(normalizedUrl, out var cached))
        {
            callback?.Invoke(cached);
            return;
        }

        if (Pending.TryGetValue(normalizedUrl, out var callbacks))
        {
            callbacks.Add(callback);
            return;
        }

        Pending[normalizedUrl] = new List<Action<Sprite>> { callback };
        runner.StartCoroutine(LoadRoutine(normalizedUrl));
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var trimmed = url.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
            return trimmed;

        var baseUrl = ApiConfig.BaseUrl.TrimEnd('/');
        return trimmed.StartsWith("/") ? baseUrl + trimmed : baseUrl + "/" + trimmed;
    }

    private static IEnumerator LoadRoutine(string url)
    {
        Sprite sprite = null;

        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            var ok = request.result == UnityWebRequest.Result.Success;
#else
            var ok = !request.isNetworkError && !request.isHttpError;
#endif
            if (ok)
            {
                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture != null)
                {
                    sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
                    Sprites[url] = sprite;
                }
            }
            else
            {
                Debug.LogWarning($"[RemoteSpriteCache] Load failed: {url} | {request.error}");
            }
        }

        if (!Pending.TryGetValue(url, out var callbacks))
            yield break;

        Pending.Remove(url);
        for (var i = 0; i < callbacks.Count; i++)
            callbacks[i]?.Invoke(sprite);
    }
}