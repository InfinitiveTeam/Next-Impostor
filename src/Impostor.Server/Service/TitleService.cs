using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Service;

/// <summary>
/// 头衔服务 - 单例，直接读写 titles.json，供 InnerPlayerControl 直接调用，无需 HTTP 回环。
/// </summary>
public class TitleService
{
    private readonly ILogger<TitleService> _logger;
    private readonly string _filePath;
    private readonly object _lock = new();

    public TitleService(ILogger<TitleService> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(Directory.GetCurrentDirectory(), "Titles", "titles.json");
        EnsureFile();
    }

    /// <summary>查询玩家头衔，无头衔返回 null。</summary>
    public string? GetTitle(string friendCode)
    {
        if (string.IsNullOrEmpty(friendCode)) return null;
        try
        {
            var data = Load();
            if (data.PlayerTitles.TryGetValue(friendCode, out var t) && !string.IsNullOrEmpty(t.Title))
            {
                _logger.LogInformation("玩家:{FriendCode}的头衔为{Title}", friendCode, t.Title);
                return t.Title;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取头衔失败: {FriendCode}", friendCode);
        }
        return null;
    }

    /// <summary>添加或更新头衔。</summary>
    public bool SetTitle(string friendCode, string title, string addedBy = "admin")
    {
        try
        {
            lock (_lock)
            {
                var data = Load();
                data.PlayerTitles[friendCode] = new PlayerTitle
                {
                    FriendCode = friendCode,
                    Title = title,
                    AddedBy = addedBy,
                    AddedTime = DateTime.UtcNow,
                    IsActive = true
                };
                Save(data);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置头衔失败: {FriendCode}", friendCode);
            return false;
        }
    }

    /// <summary>删除头衔。</summary>
    public bool RemoveTitle(string friendCode)
    {
        try
        {
            lock (_lock)
            {
                var data = Load();
                if (!data.PlayerTitles.Remove(friendCode)) return false;
                Save(data);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除头衔失败: {FriendCode}", friendCode);
            return false;
        }
    }

    /// <summary>获取所有头衔。</summary>
    public Dictionary<string, PlayerTitle> GetAll()
    {
        try { return Load().PlayerTitles; }
        catch { return new Dictionary<string, PlayerTitle>(); }
    }

    private TitleStorageData Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
                return new TitleStorageData();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<TitleStorageData>(json) ?? new TitleStorageData();
        }
    }

    private void Save(TitleStorageData data)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void EnsureFile()
    {
        try
        {
            if (!File.Exists(_filePath)) Save(new TitleStorageData());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化头衔文件失败");
        }
    }
}

public class TitleStorageData
{
    public Dictionary<string, PlayerTitle> PlayerTitles { get; set; } = new();
    public DateTime LastCleanup { get; set; } = DateTime.UtcNow;
}

public class PlayerTitle
{
    public string FriendCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string AddedBy { get; set; } = string.Empty;
    public DateTime AddedTime { get; set; }
    public bool IsActive { get; set; }
}
