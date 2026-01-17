using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Impostor.Server.Http;
using Microsoft.Extensions.Logging;

public class IpLocationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AdminController> _logger;

    public IpLocationService(HttpClient httpClient, ILogger<AdminController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<string> GetLocationAsync(string ip)
    {
        if (string.IsNullOrEmpty(ip) || ip == "未知" || ip == "127.0.0.1")
            return "本地";

        try
        {
            var url = $"https://opendata.baidu.com/api.php?query={ip}&co=&resource_id=6006&oe=utf8";
            var response = await _httpClient.GetStringAsync(url);

            using var document = JsonDocument.Parse(response);
            var data = document.RootElement.GetProperty("data");
            if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            {
                var location = data[0].GetProperty("location").GetString();
                return location ?? "未知";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取IP地理位置失败: {Ip}", ip);
        }

        return "未知";
    }
}
