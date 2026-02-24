using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Impostor.Server.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Http;

[ApiController]
[Route("api/title")]
public class TitleController : ControllerBase
{
    private readonly ILogger<TitleController> _logger;
    private readonly TitleService _titleService;

    public TitleController(ILogger<TitleController> logger, TitleService titleService)
    {
        _logger = logger;
        _titleService = titleService;
    }

    /// <summary>获取玩家头衔</summary>
    [HttpGet("get/{friendCode}")]
    public IActionResult GetPlayerTitle(string friendCode)
    {
        if (string.IsNullOrEmpty(friendCode))
            return BadRequest(new TitleInfoResponse { Success = false, Message = "好友代码不能为空" });

        var title = _titleService.GetTitle(friendCode);
        return Ok(new TitleInfoResponse
        {
            Success = true,
            FriendCode = friendCode,
            Title = title ?? string.Empty,
            Message = title != null ? string.Empty : "该玩家没有头衔"
        });
    }

    /// <summary>添加或更新头衔</summary>
    [HttpPost("add")]
    public IActionResult AddTitle([FromBody] TitleRequest request)
    {
        if (string.IsNullOrEmpty(request.FriendCode) || string.IsNullOrEmpty(request.Title))
            return BadRequest(new TitleResponse { Success = false, Message = "参数不完整" });

        var ok = _titleService.SetTitle(request.FriendCode, request.Title, request.AddedBy ?? "api");
        return ok
            ? Ok(new TitleResponse { Success = true, Message = "头衔已设置" })
            : StatusCode(500, new TitleResponse { Success = false, Message = "保存失败" });
    }

    /// <summary>删除头衔</summary>
    [HttpDelete("remove/{friendCode}")]
    public IActionResult RemoveTitle(string friendCode)
    {
        if (string.IsNullOrEmpty(friendCode))
            return BadRequest(new TitleResponse { Success = false, Message = "好友代码不能为空" });

        var ok = _titleService.RemoveTitle(friendCode);
        return ok
            ? Ok(new TitleResponse { Success = true, Message = "头衔已删除" })
            : NotFound(new TitleResponse { Success = false, Message = "未找到该玩家的头衔" });
    }

    /// <summary>获取所有头衔（调试用）</summary>
    [HttpGet("debug/all")]
    public IActionResult GetAllTitles()
    {
        var all = _titleService.GetAll();
        return Ok(new { Success = true, TitleCount = all.Count, PlayerTitles = all });
    }
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

public class TitleRequest
{
    public string FriendCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AddedBy { get; set; }
}
