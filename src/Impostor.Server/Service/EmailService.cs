using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Impostor.Api.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Service
{
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly EmailConfig _emailConfig;
        private readonly HostInfoConfig _hostInfoConfig;
        private readonly IpLocationService _ipLocationService;

        public EmailService(ILogger<EmailService> logger, IOptions<EmailConfig> emailConfig,
                          IOptions<HostInfoConfig> hostInfoConfig, IpLocationService ipLocationService)
        {
            _logger = logger;
            _emailConfig = emailConfig.Value;
            _hostInfoConfig = hostInfoConfig.Value;
            _ipLocationService = ipLocationService;
        }

        public async Task SendReportEmailAsync(string reporterName, string reporterIp, int reporterId, string reporterFriendCode,
                                             int targetPlayerId, string targetPlayerName, string targetIp, string targetFriendCode,
                                             string reason, int gameCode, string gameName,
                                             string hostName, int playerCount, string platform)
        {
            if (string.IsNullOrEmpty(_hostInfoConfig.HostEmail) ||
                _hostInfoConfig.HostEmail == "example@gmail.com")
            {
                return;
            }

            try
            {
                // 获取举报者和被举报者的地理位置
                var reporterLocation = "未知";
                var targetLocation = "未知";

                if (!string.IsNullOrEmpty(reporterIp) && reporterIp != "未知")
                {
                    reporterLocation = await _ipLocationService.GetLocationAsync(reporterIp);
                }

                if (!string.IsNullOrEmpty(targetIp) && targetIp != "未知")
                {
                    targetLocation = await _ipLocationService.GetLocationAsync(targetIp);
                }

                using var smtpClient = new SmtpClient(_emailConfig.SmtpHost)
                {
                    Port = _emailConfig.SmtpPort,
                    Credentials = new NetworkCredential(_emailConfig.Username, _emailConfig.Password),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailConfig.FromEmail, "NImpostor Server"),
                    Subject = $"🚨 玩家举报通知 - 游戏 {gameCode}",
                    Body = GenerateEmailBody(reporterName, reporterIp, reporterId, reporterFriendCode,
                                           reporterLocation, targetPlayerId, targetPlayerName, targetIp,
                                           targetFriendCode, targetLocation, reason, gameCode, gameName,
                                           hostName, playerCount, platform),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(_hostInfoConfig.HostEmail);

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("The report email has been sent to: {Email}", _hostInfoConfig.HostEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email send error");
            }
        }

        private string GenerateEmailBody(string reporterName, string reporterIp, int reporterId, string reporterFriendCode,
                                       string reporterLocation, int targetPlayerId, string targetPlayerName, string targetIp,
                                       string targetFriendCode, string targetLocation, string reason, int gameCode, string gameName,
                                       string hostName, int playerCount, string platform)
        {
            return $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>玩家举报通知</title>
    <style>
        body {{
            font-family: 'Microsoft YaHei', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            margin: 0;
            padding: 20px;
            min-height: 100vh;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background: white;
            border-radius: 15px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.3);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #ff6b6b, #ee5a24);
            color: white;
            padding: 30px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 10px;
        }}
        .content {{
            padding: 30px;
        }}
        .info-card {{
            background: #f8f9fa;
            border-radius: 10px;
            padding: 20px;
            margin: 15px 0;
            border-left: 4px solid #3498db;
        }}
        .report-card {{
            background: #fff3cd;
            border-radius: 10px;
            padding: 20px;
            margin: 15px 0;
            border-left: 4px solid #ffc107;
        }}
        .detail-card {{
            background: #d1ecf1;
            border-radius: 10px;
            padding: 20px;
            margin: 15px 0;
            border-left: 4px solid #17a2b8;
        }}
        .label {{
            font-weight: bold;
            color: #2c3e50;
            display: inline-block;
            width: 120px;
        }}
        .value {{
            color: #34495e;
        }}
        .timestamp {{
            text-align: center;
            color: #7f8c8d;
            font-style: italic;
            margin-top: 20px;
        }}
        .footer {{
            background: #34495e;
            color: white;
            text-align: center;
            padding: 20px;
            font-size: 14px;
        }}
        .urgent {{
            color: #e74c3c;
            font-weight: bold;
        }}
        .ip-address {{
            font-family: 'Courier New', monospace;
            background: #2c3e50;
            color: #ecf0f1;
            padding: 2px 6px;
            border-radius: 4px;
            font-size: 12px;
        }}
        .friend-code {{
            font-family: 'Courier New', monospace;
            background: #27ae60;
            color: #ecf0f1;
            padding: 2px 6px;
            border-radius: 4px;
            font-size: 12px;
        }}
        .location {{
            font-family: 'Microsoft YaHei', sans-serif;
            background: #8e44ad;
            color: #ecf0f1;
            padding: 2px 6px;
            border-radius: 4px;
            font-size: 12px;
        }}
        .section-title {{
            color: #2c3e50;
            border-bottom: 2px solid #3498db;
            padding-bottom: 8px;
            margin-top: 0;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🚨 玩家举报通知</h1>
            <p>您的服务器收到了新的玩家举报</p>
        </div>
        
        <div class='content'>
            <div class='info-card'>
                <h3 class='section-title'>📋 举报基本信息</h3>
                <p><span class='label'>举报者:</span> <span class='value'>{reporterName} [ID: {reporterId}]</span></p>
                <p><span class='label'>好友代码:</span> <span class='friend-code'>{reporterFriendCode ?? "未知"}</span></p>
                <p><span class='label'>举报者IP:</span> <span class='ip-address'>{reporterIp}</span></p>
                <p><span class='label'>地理位置:</span> <span class='location'>{reporterLocation}</span></p>
                
                <p style='margin-top: 15px;'><span class='label'>被举报玩家:</span> <span class='value urgent'>{targetPlayerName} [ID: {targetPlayerId}]</span></p>
                <p><span class='label'>好友代码:</span> <span class='friend-code'>{targetFriendCode ?? "未知"}</span></p>
                <p><span class='label'>被举报者IP:</span> <span class='ip-address'>{targetIp}</span></p>
                <p><span class='label'>地理位置:</span> <span class='location'>{targetLocation}</span></p>
            </div>

            <div class='report-card'>
                <h3 class='section-title'>⚡ 举报详情</h3>
                <p><span class='label'>举报原因:</span> <span class='value urgent'>{reason}</span></p>
                <p><span class='label'>游戏代码:</span> <span class='value'>{gameCode}</span></p>
                <p><span class='label'>游戏名称:</span> <span class='value'>{gameName}</span></p>
            </div>

            <div class='detail-card'>
                <h3 class='section-title'>🎮 游戏环境信息</h3>
                <p><span class='label'>房主:</span> <span class='value'>{hostName}</span></p>
                <p><span class='label'>玩家人数:</span> <span class='value'>{playerCount}/10</span></p>
                <p><span class='label'>平台:</span> <span class='value'>{platform}</span></p>
            </div>

            <div class='timestamp'>
                举报时间: {DateTime.Now:yyyy年MM月dd日 HH:mm:ss}
            </div>
        </div>

        <div class='footer'>
            <p>此邮件由 NImpostor 服务器自动发送</p>
            <p>请及时处理此举报以确保游戏环境的公平性</p>
        </div>
    </div>
</body>
</html>";
        }

        public async Task SendShutdownWarningEmailAsync(string toEmail, string subject, string body)
        {
            if (string.IsNullOrEmpty(toEmail) || toEmail == "example@gmail.com")
            {
                return;
            }

            try
            {
                using var smtpClient = new SmtpClient(_emailConfig.SmtpHost)
                {
                    Port = _emailConfig.SmtpPort,
                    Credentials = new NetworkCredential(_emailConfig.Username, _emailConfig.Password),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailConfig.FromEmail, "NImpostor Server"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Shutdown warning email sent to: {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send shutdown warning email");
            }
        }
    }

    public class EmailConfig
    {
        public string SmtpHost { get; set; } = "smtp.qq.com";

        public int SmtpPort { get; set; } = 587;

        public string Username { get; set; } = "1767265134@qq.com";

        public string Password { get; set; } = "ycmrhhhraxsvfccb";

        public string FromEmail { get; set; } = "1767265134@qq.com";

    }
}
