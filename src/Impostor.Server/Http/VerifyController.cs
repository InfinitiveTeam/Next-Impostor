// VerifyController.cs
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
    [Route("api/verify")]
    public class VerifyController : ControllerBase
    {
        private readonly ILogger<VerifyController> _logger;
        private readonly string _filePath;
        private readonly object _fileLock = new object();
        private readonly Random _random = new Random();

        public VerifyController(ILogger<VerifyController> logger)
        {
            _logger = logger;

            // 设置文件路径
            _filePath = Path.Combine(Directory.GetCurrentDirectory(), "Verify", "info.json");
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
                    _logger.LogInformation($"创建验证目录: {directory}");
                }

                if (!System.IO.File.Exists(_filePath))
                {
                    var initialData = new VerifyStorageData
                    {
                        Sessions = new Dictionary<string, VerifySession>(),
                        VerifiedPlayers = new Dictionary<string, VerifiedPlayer>(),
                        LastCleanup = DateTime.UtcNow
                    };

                    SaveData(initialData);
                    _logger.LogInformation($"创建验证存储文件: {_filePath}");
                }
                else
                {
                    _logger.LogInformation($"验证存储文件已存在: {_filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化验证存储文件失败");
            }
        }

        /// <summary>
        /// 读取存储数据
        /// </summary>
        private VerifyStorageData LoadData()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!System.IO.File.Exists(_filePath))
                    {
                        return new VerifyStorageData
                        {
                            Sessions = new Dictionary<string, VerifySession>(),
                            VerifiedPlayers = new Dictionary<string, VerifiedPlayer>(),
                            LastCleanup = DateTime.UtcNow
                        };
                    }

                    var json = System.IO.File.ReadAllText(_filePath);
                    var data = JsonSerializer.Deserialize<VerifyStorageData>(json);
                    return data ?? new VerifyStorageData();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "读取验证数据失败");
                    return new VerifyStorageData
                    {
                        Sessions = new Dictionary<string, VerifySession>(),
                        VerifiedPlayers = new Dictionary<string, VerifiedPlayer>(),
                        LastCleanup = DateTime.UtcNow
                    };
                }
            }
        }

        /// <summary>
        /// 保存存储数据
        /// </summary>
        private void SaveData(VerifyStorageData data)
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
                    _logger.LogError(ex, "保存验证数据失败");
                }
            }
        }

        /// <summary>
        /// 生成8位不重复验证码
        /// </summary>
        private string GenerateVerifyCode()
        {
            return _random.Next(10000000, 99999999).ToString();
        }

        /// <summary>
        /// 创建验证会话
        /// </summary>
        [HttpPost("create")]
        public IActionResult CreateVerifySession([FromBody] CreateVerifyRequest request)
        {
            try
            {
                _logger.LogInformation($"收到创建验证请求: QQ={request.QQNumber}, FriendCode={request.FriendCode}");

                if (string.IsNullOrEmpty(request.QQNumber) || string.IsNullOrEmpty(request.FriendCode))
                {
                    return BadRequest(new VerifyResponse
                    {
                        Success = false,
                        Message = "QQ号码和好友代码不能为空"
                    });
                }

                var data = LoadData();

                // 检查是否已经验证过
                if (data.VerifiedPlayers.ContainsKey(request.QQNumber))
                {
                    _logger.LogInformation($"QQ号已验证过: {request.QQNumber}");
                    return Ok(new VerifyResponse
                    {
                        Success = false,
                        Message = "该QQ号已完成验证",
                        VerifyCode = null
                    });
                }

                // 生成8位不重复验证码
                string verifyCode;
                int attempts = 0;
                do
                {
                    verifyCode = GenerateVerifyCode();
                    attempts++;
                    if (attempts > 100) // 防止无限循环
                    {
                        throw new Exception("无法生成唯一验证码");
                    }
                } while (data.Sessions.ContainsKey(verifyCode));

                var session = new VerifySession
                {
                    QQNumber = request.QQNumber,
                    FriendCode = request.FriendCode,
                    VerifyCode = verifyCode,
                    CreatedTime = DateTime.UtcNow,
                    IsVerified = false,
                    GameCode = request.GameCode
                };

                data.Sessions[verifyCode] = session;
                SaveData(data);

                _logger.LogInformation($"创建验证会话成功: QQ={request.QQNumber}, Code={verifyCode}");

                return Ok(new VerifyResponse
                {
                    Success = true,
                    Message = $"请私聊XtremeBot并输入 /验证AU {verifyCode}\n该验证码十分钟内有效\n若没有，请添加：XtremeBot(2352951844)",
                    VerifyCode = verifyCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建验证会话失败");
                return StatusCode(500, new VerifyResponse
                {
                    Success = false,
                    Message = "系统错误，请稍后重试"
                });
            }
        }

        /// <summary>
        /// 检查验证状态
        /// </summary>
        [HttpGet("status/{verifyCode}")]
        public IActionResult GetVerifyStatus(string verifyCode)
        {
            try
            {
                _logger.LogInformation($"查询验证状态: Code={verifyCode}");

                if (string.IsNullOrEmpty(verifyCode) || !verifyCode.All(char.IsDigit) || verifyCode.Length != 8)
                {
                    return BadRequest(new VerifyStatusResponse
                    {
                        Success = false,
                        Message = "验证码格式不正确"
                    });
                }

                var data = LoadData();

                if (data.Sessions.TryGetValue(verifyCode, out var session))
                {
                    // 检查是否过期（24小时）
                    if (DateTime.UtcNow - session.CreatedTime > TimeSpan.FromMinutes(10))
                    {
                        data.Sessions.Remove(verifyCode);
                        SaveData(data);
                        _logger.LogInformation($"验证码已过期: {verifyCode}");
                        return NotFound(new VerifyStatusResponse
                        {
                            Success = false,
                            Message = "验证码已过期"
                        });
                    }

                    _logger.LogInformation($"找到验证会话: Code={verifyCode}, QQ={session.QQNumber}, Verified={session.IsVerified}");
                    return Ok(new VerifyStatusResponse
                    {
                        Success = true,
                        QQNumber = session.QQNumber,
                        FriendCode = session.FriendCode,
                        IsVerified = session.IsVerified,
                        CreatedTime = session.CreatedTime,
                        VerifiedTime = session.VerifiedTime
                    });
                }

                _logger.LogInformation($"验证码不存在: {verifyCode}");
                return NotFound(new VerifyStatusResponse
                {
                    Success = false,
                    Message = "验证码不存在"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询验证状态失败");
                return StatusCode(500, new VerifyStatusResponse
                {
                    Success = false,
                    Message = "系统错误"
                });
            }
        }

        /// <summary>
        /// 完成验证
        /// </summary>
        [HttpPost("complete")]
        public IActionResult CompleteVerify([FromBody] CompleteVerifyRequest request)
        {
            try
            {
                _logger.LogInformation($"收到完成验证请求: Code={request.VerifyCode}, VerifiedBy={request.VerifiedBy}");

                if (string.IsNullOrEmpty(request.VerifyCode))
                {
                    return BadRequest(new VerifyResponse
                    {
                        Success = false,
                        Message = "验证码不能为空"
                    });
                }

                var data = LoadData();

                if (!data.Sessions.TryGetValue(request.VerifyCode, out var session))
                {
                    _logger.LogInformation($"验证码不存在: {request.VerifyCode}");
                    return NotFound(new VerifyResponse
                    {
                        Success = false,
                        Message = "验证码不存在"
                    });
                }

                // 检查是否过期
                if (DateTime.UtcNow - session.CreatedTime > TimeSpan.FromHours(24))
                {
                    data.Sessions.Remove(request.VerifyCode);
                    SaveData(data);
                    _logger.LogInformation($"验证码已过期: {request.VerifyCode}");
                    return NotFound(new VerifyResponse
                    {
                        Success = false,
                        Message = "验证码已过期"
                    });
                }

                if (session.IsVerified)
                {
                    _logger.LogInformation($"验证码已完成验证: {request.VerifyCode}");
                    return Ok(new VerifyResponse
                    {
                        Success = true,
                        Message = "该验证码已完成验证"
                    });
                }

                // 更新验证状态
                session.IsVerified = true;
                session.VerifiedTime = DateTime.UtcNow;
                session.VerifiedBy = request.VerifiedBy;

                // 保存到已验证玩家
                var verifiedPlayer = new VerifiedPlayer
                {
                    QQNumber = session.QQNumber,
                    FriendCode = session.FriendCode,
                    VerifiedTime = session.VerifiedTime.Value,
                    VerifyCode = session.VerifyCode,
                    GameCode = session.GameCode
                };

                data.VerifiedPlayers[session.QQNumber] = verifiedPlayer;
                SaveData(data);

                _logger.LogInformation($"验证完成: QQ={session.QQNumber}, Code={session.VerifyCode}, VerifiedBy={request.VerifiedBy}");

                // 清理过期会话
                CleanupExpiredSessions(data);

                return Ok(new VerifyResponse
                {
                    Success = true,
                    Message = "验证成功",
                    IsVerifySucceed = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "完成验证失败");
                return StatusCode(500, new VerifyResponse
                {
                    Success = false,
                    Message = "系统错误"
                });
            }
        }

        /// <summary>
        /// 检查玩家验证状态
        /// </summary>
        [HttpGet("player/{qqNumber}")]
        public IActionResult GetPlayerStatus(string qqNumber)
        {
            try
            {
                _logger.LogInformation($"查询玩家验证状态: QQ={qqNumber}");

                if (string.IsNullOrEmpty(qqNumber))
                {
                    return BadRequest(new PlayerStatusResponse
                    {
                        IsVerified = false,
                        Message = "QQ号码不能为空"
                    });
                }

                var data = LoadData();

                if (data.VerifiedPlayers.TryGetValue(qqNumber, out var player))
                {
                    _logger.LogInformation($"玩家已验证: {qqNumber}");
                    return Ok(new PlayerStatusResponse
                    {
                        IsVerified = true,
                        QQNumber = player.QQNumber,
                        FriendCode = player.FriendCode,
                        VerifiedTime = player.VerifiedTime,
                        VerifyCode = player.VerifyCode
                    });
                }

                _logger.LogInformation($"玩家未验证: {qqNumber}");
                return Ok(new PlayerStatusResponse
                {
                    IsVerified = false,
                    QQNumber = qqNumber,
                    Message = "该QQ号尚未完成验证"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询玩家验证状态失败");
                return StatusCode(500, new PlayerStatusResponse
                {
                    IsVerified = false,
                    Message = "系统错误"
                });
            }
        }

        /// <summary>
        /// 获取所有验证会话（调试用）
        /// </summary>
        [HttpGet("debug/sessions")]
        public IActionResult GetDebugSessions()
        {
            try
            {
                var data = LoadData();
                return Ok(new
                {
                    Success = true,
                    SessionCount = data.Sessions.Count,
                    VerifiedPlayerCount = data.VerifiedPlayers.Count,
                    Sessions = data.Sessions,
                    VerifiedPlayers = data.VerifiedPlayers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        /// <summary>
        /// 清理过期会话（24小时）
        /// </summary>
        private void CleanupExpiredSessions(VerifyStorageData data)
        {
            try
            {
                // 每1小时清理一次
                if (DateTime.UtcNow - data.LastCleanup < TimeSpan.FromHours(1))
                    return;

                var expiredCodes = data.Sessions
                    .Where(kv => DateTime.UtcNow - kv.Value.CreatedTime > TimeSpan.FromHours(24))
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var code in expiredCodes)
                {
                    data.Sessions.Remove(code);
                }

                // 同时清理旧的已验证记录（保留30天）
                var oldVerifiedThreshold = DateTime.UtcNow.AddDays(-30);
                var oldVerifiedPlayers = data.VerifiedPlayers
                    .Where(kv => kv.Value.VerifiedTime < oldVerifiedThreshold)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var qq in oldVerifiedPlayers)
                {
                    data.VerifiedPlayers.Remove(qq);
                }

                data.LastCleanup = DateTime.UtcNow;
                SaveData(data);

                if (expiredCodes.Count > 0 || oldVerifiedPlayers.Count > 0)
                {
                    _logger.LogInformation($"清理完成: {expiredCodes.Count}个过期会话, {oldVerifiedPlayers.Count}个旧验证记录");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理过期会话失败");
            }
        }
    }

    // 存储数据结构
    public class VerifyStorageData
    {
        public Dictionary<string, VerifySession> Sessions { get; set; } = new Dictionary<string, VerifySession>();
        public Dictionary<string, VerifiedPlayer> VerifiedPlayers { get; set; } = new Dictionary<string, VerifiedPlayer>();
        public DateTime LastCleanup { get; set; } = DateTime.UtcNow;
    }

    // 请求和响应DTO类
    public class CreateVerifyRequest
    {
        public string QQNumber { get; set; } = string.Empty;
        public string FriendCode { get; set; } = string.Empty;
        public string GameCode { get; set; } = string.Empty;
    }

    public class CompleteVerifyRequest
    {
        public string VerifyCode { get; set; } = string.Empty;
        public string VerifiedBy { get; set; } = string.Empty;
    }

    public class VerifyResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string VerifyCode { get; set; } = string.Empty;
        public bool IsVerifySucceed { get; set; }
    }

    public class VerifyStatusResponse : VerifyResponse
    {
        public string QQNumber { get; set; } = string.Empty;
        public string FriendCode { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? VerifiedTime { get; set; }
    }

    public class PlayerStatusResponse
    {
        public bool IsVerified { get; set; }
        public string QQNumber { get; set; } = string.Empty;
        public string FriendCode { get; set; } = string.Empty;
        public DateTime? VerifiedTime { get; set; }
        public string VerifyCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class VerifySession
    {
        public string QQNumber { get; set; } = string.Empty;
        public string FriendCode { get; set; } = string.Empty;
        public string VerifyCode { get; set; } = string.Empty;
        public string GameCode { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public bool IsVerified { get; set; }
        public DateTime? VerifiedTime { get; set; }
        public string VerifiedBy { get; set; } = string.Empty;
    }

    public class VerifiedPlayer
    {
        public string QQNumber { get; set; } = string.Empty;
        public string FriendCode { get; set; } = string.Empty;
        public string VerifyCode { get; set; } = string.Empty;
        public string GameCode { get; set; } = string.Empty;
        public DateTime VerifiedTime { get; set; }
    }
}
