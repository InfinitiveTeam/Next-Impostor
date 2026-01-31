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
        private readonly HostInfoConfig _hostInfoConfig;
        private readonly IpLocationService _ipLocationService;

        public EmailService(ILogger<EmailService> logger, IOptions<HostInfoConfig> hostInfoConfig, IpLocationService ipLocationService)
        {
            _logger = logger;
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
                // Get the geographical location of the reporter and the reported player
                var reporterLocation = "Unknown";
                var targetLocation = "Unknown";

                if (!string.IsNullOrEmpty(reporterIp) && reporterIp != "Unknown")
                {
                    reporterLocation = await _ipLocationService.GetLocationAsync(reporterIp);
                }

                if (!string.IsNullOrEmpty(targetIp) && targetIp != "Unknown")
                {
                    targetLocation = await _ipLocationService.GetLocationAsync(targetIp);
                }

                using var smtpClient = new SmtpClient(_hostInfoConfig.SmtpHost)
                {
                    Port = _hostInfoConfig.SmtpPort,
                    Credentials = new NetworkCredential(_hostInfoConfig.Username, _hostInfoConfig.Password),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_hostInfoConfig.FromEmail, "NImpostor Server"),
                    Subject = $"🚨 Player Report Notification - Game {gameCode}",
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
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Player Report Notification</title>
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
            <h1>🚨 Player Report Notification</h1>
            <p>Your server has received a new player report</p>
        </div>
        
        <div class='content'>
            <div class='info-card'>
                <h3 class='section-title'>📋 Basic Report Information</h3>
                <p><span class='label'>Reporter:</span> <span class='value'>{reporterName} [ID: {reporterId}]</span></p>
                <p><span class='label'>Friend Code:</span> <span class='friend-code'>{reporterFriendCode ?? "Unknown"}</span></p>
                <p><span class='label'>Reporter IP:</span> <span class='ip-address'>{reporterIp}</span></p>
                <p><span class='label'>Location:</span> <span class='location'>{reporterLocation}</span></p>
                
                <p style='margin-top: 15px;'><span class='label'>Reported Player:</span> <span class='value urgent'>{targetPlayerName} [ID: {targetPlayerId}]</span></p>
                <p><span class='label'>Friend Code:</span> <span class='friend-code'>{targetFriendCode ?? "Unknown"}</span></p>
                <p><span class='label'>Reported Player IP:</span> <span class='ip-address'>{targetIp}</span></p>
                <p><span class='label'>Location:</span> <span class='location'>{targetLocation}</span></p>
            </div>

            <div class='report-card'>
                <h3 class='section-title'>⚡ Report Details</h3>
                <p><span class='label'>Reason:</span> <span class='value urgent'>{reason}</span></p>
                <p><span class='label'>Game Code:</span> <span class='value'>{gameCode}</span></p>
                <p><span class='label'>Game Name:</span> <span class='value'>{gameName}</span></p>
            </div>

            <div class='detail-card'>
                <h3 class='section-title'>🎮 Game Environment Information</h3>
                <p><span class='label'>Host:</span> <span class='value'>{hostName}</span></p>
                <p><span class='label'>Player Count:</span> <span class='value'>{playerCount}/10</span></p>
                <p><span class='label'>Platform:</span> <span class='value'>{platform}</span></p>
            </div>

            <div class='timestamp'>
                Report Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
            </div>
        </div>

        <div class='footer'>
            <p>This email was automatically sent by the NImpostor Server</p>
            <p>Please handle this report promptly to ensure fairness in the game environment</p>
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
                using var smtpClient = new SmtpClient(_hostInfoConfig.SmtpHost)
                {
                    Port = _hostInfoConfig.SmtpPort,
                    Credentials = new NetworkCredential(_hostInfoConfig.Username, _hostInfoConfig.Password),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_hostInfoConfig.FromEmail, "NImpostor Server"),
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
}
