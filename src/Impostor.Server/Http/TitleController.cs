using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Http
{
    [ApiController]
    [Route("api/title")]
    public class TitleController : ControllerBase
    {
        private readonly ILogger<TitleController> _logger;
        private readonly string _filePath;
        private readonly object _fileLock = new object();

        public TitleController(ILogger<TitleController> logger)
        {
            _logger = logger;
            _filePath = Path.Combine(Directory.GetCurrentDirectory(), "Titles", "titles.json");
            InitializeStorageFile();
        }

        /// <summary>
        /// 初始化存储文件
        /// </summary>
        private void InitializeStorageFile()
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogInformation($"创建头衔目录: {directory}");
                }

                if (!System.IO.File.Exists(_filePath))
                {
                    var initialData = new TitleStorageData
                    {
                        PlayerTitles = new Dictionary<string, PlayerTitle>(),
                        LastCleanup = DateTime.UtcNow
                    };

                    SaveData(initialData);
                    _logger.LogInformation($"创建头衔存储文件: {_filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化头衔存储文件失败");
            }
        }

        /// <summary>
        /// 读取存储数据
        /// </summary>
        private TitleStorageData LoadData()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!System.IO.File.Exists(_filePath))
                    {
                        return new TitleStorageData
                        {
                            PlayerTitles = new Dictionary<string, PlayerTitle>(),
                            LastCleanup = DateTime.UtcNow
                        };
                    }

                    var json = System.IO.File.ReadAllText(_filePath);
                    var data = JsonSerializer.Deserialize<TitleStorageData>(json);
                    return data ?? new TitleStorageData();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "读取头衔数据失败");
                    return new TitleStorageData();
                }
            }
        }

        /// <summary>
        /// 保存存储数据
        /// </summary>
        private void SaveData(TitleStorageData data)
        {
            lock (_fileLock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(data, options);
                    System.IO.File.WriteAllText(_filePath, json);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "保存头衔数据失败");
                }
            }
        }

        /// <summary>
        /// 为玩家添加头衔
        /// </summary>
        [HttpPost("add")]
        public IActionResult AddPlayerTitle([FromBody] AddTitleRequest request)
        {
            try
            {
                _logger.LogInformation($"添加头衔请求: FriendCode={request.FriendCode}, Title={request.Title}");

                if (string.IsNullOrEmpty(request.FriendCode) || string.IsNullOrEmpty(request.Title))
                {
                    return BadRequest(new TitleResponse
                    {
                        Success = false,
                        Message = "好友代码和头衔不能为空"
                    });
                }

                // 验证头衔长度（最大10个字符）
                if (request.Title.Length > 10)
                {
                    return BadRequest(new TitleResponse
                    {
                        Success = false,
                        Message = "头衔长度不能超过10个字符"
                    });
                }

                var data = LoadData();

                var playerTitle = new PlayerTitle
                {
                    FriendCode = request.FriendCode,
                    Title = request.Title,
                    AddedBy = request.AddedBy,
                    AddedTime = DateTime.UtcNow,
                    IsActive = true
                };

                data.PlayerTitles[request.FriendCode] = playerTitle;
                SaveData(data);

                _logger.LogInformation($"头衔添加成功: {request.FriendCode} -> {request.Title}");

                return Ok(new TitleResponse
                {
                    Success = true,
                    Message = $"已为好友代码 {request.FriendCode} 添加头衔: {request.Title}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加头衔失败");
                return StatusCode(500, new TitleResponse
                {
                    Success = false,
                    Message = "系统错误，请稍后重试"
                });
            }
        }

        /// <summary>
        /// 移除玩家头衔
        /// </summary>
        [HttpDelete("remove/{friendCode}")]
        public IActionResult RemovePlayerTitle(string friendCode)
        {
            try
            {
                _logger.LogInformation($"移除头衔请求: FriendCode={friendCode}");

                if (string.IsNullOrEmpty(friendCode))
                {
                    return BadRequest(new TitleResponse
                    {
                        Success = false,
                        Message = "好友代码不能为空"
                    });
                }

                var data = LoadData();

                if (data.PlayerTitles.ContainsKey(friendCode))
                {
                    data.PlayerTitles.Remove(friendCode);
                    SaveData(data);

                    _logger.LogInformation($"头衔移除成功: {friendCode}");
                    return Ok(new TitleResponse
                    {
                        Success = true,
                        Message = $"已移除好友代码 {friendCode} 的头衔"
                    });
                }
                else
                {
                    return NotFound(new TitleResponse
                    {
                        Success = false,
                        Message = "未找到该好友代码的头衔记录"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "移除头衔失败");
                return StatusCode(500, new TitleResponse
                {
                    Success = false,
                    Message = "系统错误，请稍后重试"
                });
            }
        }

        /// <summary>
        /// 获取玩家头衔
        /// </summary>
        [HttpGet("get/{friendCode}")]
        public IActionResult GetPlayerTitle(string friendCode)
        {
            try
            {
                _logger.LogDebug($"获取头衔请求: FriendCode={friendCode}");

                if (string.IsNullOrEmpty(friendCode))
                {
                    return BadRequest(new TitleInfoResponse
                    {
                        Success = false,
                        Message = "好友代码不能为空"
                    });
                }

                var data = LoadData();

                if (data.PlayerTitles.TryGetValue(friendCode, out var title))
                {
                    return Ok(new TitleInfoResponse
                    {
                        Success = true,
                        FriendCode = title.FriendCode,
                        Title = title.Title,
                        AddedBy = title.AddedBy,
                        AddedTime = title.AddedTime,
                        IsActive = title.IsActive
                    });
                }
                else
                {
                    return Ok(new TitleInfoResponse
                    {
                        Success = true,
                        FriendCode = friendCode,
                        Title = null, // 没有头衔
                        Message = "该玩家没有头衔"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取头衔失败");
                return StatusCode(500, new TitleInfoResponse
                {
                    Success = false,
                    Message = "系统错误"
                });
            }
        }

        /// <summary>
        /// 获取所有头衔（调试用）
        /// </summary>
        [HttpGet("debug/all")]
        public IActionResult GetAllTitles()
        {
            try
            {
                var data = LoadData();
                return Ok(new
                {
                    Success = true,
                    TitleCount = data.PlayerTitles.Count,
                    PlayerTitles = data.PlayerTitles
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }
    }

    // 存储数据结构
    public class TitleStorageData
    {
        public Dictionary<string, PlayerTitle> PlayerTitles { get; set; } = new Dictionary<string, PlayerTitle>();
        public DateTime LastCleanup { get; set; } = DateTime.UtcNow;
    }

    // 请求和响应DTO类
    public class AddTitleRequest
    {
        public string FriendCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AddedBy { get; set; } = string.Empty;
    }

    public class TitleResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class TitleInfoResponse : TitleResponse
    {
        public string FriendCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AddedBy { get; set; } = string.Empty;
        public DateTime AddedTime { get; set; }
        public bool IsActive { get; set; }
    }

    public class PlayerTitle
    {
        public string FriendCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AddedBy { get; set; } = string.Empty;
        public DateTime AddedTime { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
