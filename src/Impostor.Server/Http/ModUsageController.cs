using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Impostor.Server.Http
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModUsageController : ControllerBase
    {
        private readonly string dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ModUsesData");
        private readonly ILogger _logger;

        public ModUsageController(ILogger logger)
        {
            _logger = logger;

            // Ensure data directory exists
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
                _logger.Information("Created mod usage data directory: {DataDirectory}", dataDirectory);
            }
        }

        [HttpGet("register")]
        public async Task<IActionResult> RegisterMod(string modName)
        {
            // Log request information
            _logger.Information("Received mod registration request: {ModName}",
                modName);

            if (string.IsNullOrEmpty(modName))
            {
                _logger.Warning("Mod registration request failed: mod name is empty");
                return BadRequest("Mod name is required");
            }

            // Clean mod name to ensure it's a valid filename
            var cleanModName = CleanModName(modName);
            var filePath = Path.Combine(dataDirectory, $"{cleanModName}.txt");

            int usageCount = 1;
            bool isNewMod = false;

            // Check if file exists
            if (System.IO.File.Exists(filePath))
            {
                // Read existing count
                var content = await System.IO.File.ReadAllTextAsync(filePath);
                if (int.TryParse(content, out int existingCount))
                {
                    usageCount = existingCount + 1;
                    _logger.Debug("Mod {ModName} already exists, current usage count: {ExistingCount}, updating to: {NewCount}",
                        modName, existingCount, usageCount);
                }
                else
                {
                    _logger.Warning("Invalid data file format for mod {ModName}, resetting count", modName);
                }
            }
            else
            {
                isNewMod = true;
                _logger.Information("Detected new mod: {ModName}", modName);
            }

            // Write new count
            try
            {
                await System.IO.File.WriteAllTextAsync(filePath, usageCount.ToString());
                _logger.Debug("Successfully wrote mod {ModName} usage count: {UsageCount}", modName, usageCount);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred while writing mod {ModName} usage count", modName);
                return StatusCode(500, "Failed to save mod usage data");
            }

            // Log successful registration
            if (isNewMod)
            {
                _logger.Information("Successfully registered new mod: {ModName}, initial usage count: 1", modName);
            }
            else
            {
                _logger.Information("Updated mod {ModName} usage count: {UsageCount}", modName, usageCount);
            }

            return Ok(usageCount.ToString());
        }

        [HttpGet("usage/{modName}")]
        public IActionResult GetModUsage(string modName)
        {
            _logger.Debug("Received mod usage count query request: {ModName}", modName);

            if (string.IsNullOrEmpty(modName))
            {
                return BadRequest("Mod name is required");
            }

            // Clean mod name to ensure it's a valid filename
            var cleanModName = CleanModName(modName);
            var filePath = Path.Combine(dataDirectory, $"{cleanModName}.txt");

            int usageCount = 0;

            // Check if file exists
            if (System.IO.File.Exists(filePath))
            {
                // Read existing count
                var content = System.IO.File.ReadAllText(filePath);
                if (int.TryParse(content, out int existingCount))
                {
                    usageCount = existingCount;
                    _logger.Debug("Returning mod {ModName} usage count: {UsageCount}", modName, usageCount);
                }
                else
                {
                    _logger.Warning("Invalid data file format for mod {ModName}", modName);
                    return NotFound($"Invalid data format for mod: {modName}");
                }
            }
            else
            {
                _logger.Debug("Mod {ModName} not found", modName);
                return NotFound($"Mod not found: {modName}");
            }

            // Return result in JSON format
            var result = new
            {
                modName,
                usageCount,
                lastUpdated = System.IO.File.GetLastWriteTime(filePath).ToString("yyyy-MM-dd HH:mm:ss")
            };

            return Ok(result);
        }

        [HttpGet("stats")]
        public IActionResult GetModStats()
        {
            _logger.Debug("Mod usage statistics page was accessed");

            // Get language preference from query parameters, default to English
            var lang = Request.Query.ContainsKey("lang") ? Request.Query["lang"].ToString() : "en";
            _logger.Debug("Request language preference: {Language}", lang);

            if (!Directory.Exists(dataDirectory))
            {
                _logger.Warning("Mod statistics data directory does not exist");
                return Ok(lang == "zh" ?
                    "<html><body><h1>模组使用统计</h1><p>暂无模组使用数据可用</p></body></html>" :
                    "<html><body><h1>Mod Usage Statistics</h1><p>No mod usage data available</p></body></html>");
            }

            var modFiles = Directory.GetFiles(dataDirectory, "*.txt");

            // Build HTML response
            var html = new System.Text.StringBuilder();

            // HTML head and styles
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"" + lang + "\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine("    <title>" + (lang == "zh" ? "模组使用统计" : "Mod Usage Statistics") + "</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 40px; background-color: #f5f5f5; }");
            html.AppendLine("        .container { max-width: 1000px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }");
            html.AppendLine("        h1 { color: #333; border-bottom: 2px solid #4CAF50; padding-bottom: 10px; }");
            html.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            html.AppendLine("        th, td { padding: 12px 15px; text-align: left; border-bottom: 1px solid #ddd; }");
            html.AppendLine("        th { background-color: #4CAF50; color: white; }");
            html.AppendLine("        tr:hover { background-color: #f5f5f5; }");
            html.AppendLine("        .language-switcher { margin-bottom: 20px; }");
            html.AppendLine("        .language-switcher a { margin-right: 10px; padding: 5px 10px; background: #e0e0e0; border-radius: 4px; text-decoration: none; color: #333; }");
            html.AppendLine("        .language-switcher a.active { background: #4CAF50; color: white; }");
            html.AppendLine("        .stats-summary { background: #e8f5e9; padding: 15px; border-radius: 4px; margin-bottom: 20px; }");
            html.AppendLine("        .api-example { background: #e3f2fd; padding: 15px; border-radius: 4px; margin-top: 30px; }");
            html.AppendLine("        code { background: #f5f5f5; padding: 2px 5px; border-radius: 3px; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("    <div class=\"container\">");

            // Language switcher
            html.AppendLine("        <div class=\"language-switcher\">");
            html.AppendLine("            <a href=\"?lang=en\"" + (lang == "en" ? " class=\"active\"" : "") + ">English</a>");
            html.AppendLine("            <a href=\"?lang=zh\"" + (lang == "zh" ? " class=\"active\"" : "") + ">中文</a>");
            html.AppendLine("        </div>");

            // Title
            html.AppendLine("        <h1>" + (lang == "zh" ? "模组使用统计" : "Mod Usage Statistics") + "</h1>");

            // Statistics summary
            int totalUses = 0;
            foreach (var file in modFiles)
            {
                try
                {
                    var content = System.IO.File.ReadAllText(file);
                    if (int.TryParse(content, out int count))
                    {
                        totalUses += count;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error reading mod statistics file: {FilePath}", file);
                }
            }

            html.AppendLine("        <div class=\"stats-summary\">");
            html.AppendLine("            <p>" + (lang == "zh" ?
                $"共追踪 {modFiles.Length} 个模组，总使用次数: {totalUses}" :
                $"Tracking {modFiles.Length} mods, Total uses: {totalUses}") + "</p>");
            html.AppendLine("        </div>");

            // Table start
            html.AppendLine("        <table>");
            html.AppendLine("            <thead>");
            html.AppendLine("                <tr>");
            html.AppendLine("                    <th>" + (lang == "zh" ? "模组名称" : "Mod Name") + "</th>");
            html.AppendLine("                    <th>" + (lang == "zh" ? "使用次数" : "Usage Count") + "</th>");
            html.AppendLine("                </tr>");
            html.AppendLine("            </thead>");
            html.AppendLine("            <tbody>");

            // Table content
            foreach (var file in modFiles)
            {
                try
                {
                    var modName = Path.GetFileNameWithoutExtension(file);
                    var usageCount = System.IO.File.ReadAllText(file);
                    html.AppendLine("                <tr>");
                    html.AppendLine("                    <td>" + modName + "</td>");
                    html.AppendLine("                    <td>" + usageCount + "</td>");
                    html.AppendLine("                </tr>");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error reading mod statistics file: {FilePath}", file);
                    html.AppendLine("                <tr>");
                    html.AppendLine("                    <td colspan=\"2\" style=\"color: red;\">" +
                        (lang == "zh" ? "读取错误" : "Read Error") + ": " + file + "</td>");
                    html.AppendLine("                </tr>");
                }
            }

            // Table end
            html.AppendLine("            </tbody>");
            html.AppendLine("        </table>");

            // API usage example
            html.AppendLine("        <div class=\"api-example\">");
            html.AppendLine("            <h2>" + (lang == "zh" ? "API 使用示例" : "API Usage Example") + "</h2>");
            html.AppendLine("            <p>" + (lang == "zh" ?
                "您可以使用以下API获取特定模组的使用次数:" :
                "You can use the following API to get usage count for a specific mod:") + "</p>");
            html.AppendLine("            <p><code>GET /api/modusage/usage/{modName}</code></p>");
            html.AppendLine("            <p>" + (lang == "zh" ?
                "示例响应:" : "Example response:") + "</p>");
            html.AppendLine("            <pre><code>{\n  \"modName\": \"TheOtherRoles\",\n  \"usageCount\": 42,\n  \"lastUpdated\": \"2023-06-15 14:30:25\"\n}</code></pre>");

            // Simple HTML example
            html.AppendLine("            <h3>" + (lang == "zh" ? "HTML 集成示例" : "HTML Integration Example") + "</h3>");
            html.AppendLine("            <pre><code>&lt;div id=\"mod-stats\"&gt;&lt;/div&gt;\n&lt;script&gt;\n  fetch('/api/modusage/usage/TheOtherRoles')\n    .then(response => response.json())\n    .then(data => {\n      document.getElementById('mod-stats').innerHTML = \n        `&lt;p&gt;模组 ${data.modName} 已被使用 ${data.usageCount} 次&lt;/p&gt;`;\n    })\n    .catch(error => console.error('Error:', error));\n&lt;/script&gt;</code></pre>");
            html.AppendLine("        </div>");

            html.AppendLine("    </div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return Content(html.ToString(), "text/html");
        }

        private string CleanModName(string modName)
        {
            // Record original mod name
            var originalName = modName;

            // Remove or replace invalid characters in filename
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                modName = modName.Replace(c, '_');
            }

            // Log warning if name was modified
            if (originalName != modName)
            {
                _logger.Warning("Mod name contained invalid characters, cleaned: {Original} -> {Cleaned}",
                    originalName, modName);
            }

            return modName;
        }
    }
}
