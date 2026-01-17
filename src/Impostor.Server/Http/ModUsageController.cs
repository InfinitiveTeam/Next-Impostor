using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Threading.Tasks;
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

            // 确保数据目录存在
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
                _logger.Information("创建模组使用数据目录: {DataDirectory}", dataDirectory);
            }
        }

        [HttpGet("register")]
        public async Task<IActionResult> RegisterMod(string modName)
        {
            // 记录请求信息
            _logger.Information("收到模组注册请求: {ModName}",
                modName);

            if (string.IsNullOrEmpty(modName))
            {
                _logger.Warning("模组注册请求失败: 模组名称为空");
                return BadRequest("Mod name is required");
            }

            // 清理mod名称，确保它是有效的文件名
            var cleanModName = CleanModName(modName);
            var filePath = Path.Combine(dataDirectory, $"{cleanModName}.txt");

            int usageCount = 1;
            bool isNewMod = false;

            // 检查文件是否存在
            if (System.IO.File.Exists(filePath))
            {
                // 读取现有计数
                var content = await System.IO.File.ReadAllTextAsync(filePath);
                if (int.TryParse(content, out int existingCount))
                {
                    usageCount = existingCount + 1;
                    _logger.Debug("模组 {ModName} 已存在，当前使用次数: {ExistingCount}, 更新为: {NewCount}",
                        modName, existingCount, usageCount);
                }
                else
                {
                    _logger.Warning("模组 {ModName} 的数据文件格式无效，重置计数", modName);
                }
            }
            else
            {
                isNewMod = true;
                _logger.Information("检测到新模组: {ModName}", modName);
            }

            // 写入新的计数
            try
            {
                await System.IO.File.WriteAllTextAsync(filePath, usageCount.ToString());
                _logger.Debug("成功写入模组 {ModName} 的使用次数: {UsageCount}", modName, usageCount);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "写入模组 {ModName} 使用次数时发生错误", modName);
                return StatusCode(500, "Failed to save mod usage data");
            }

            // 记录成功注册
            if (isNewMod)
            {
                _logger.Information("成功注册新模组: {ModName}, 初始使用次数: 1", modName);
            }
            else
            {
                _logger.Information("更新模组 {ModName} 使用次数: {UsageCount}", modName, usageCount);
            }

            return Ok(usageCount.ToString());
        }

        [HttpGet("usage/{modName}")]
        public IActionResult GetModUsage(string modName)
        {
            _logger.Debug("收到模组使用次数查询请求: {ModName}", modName);

            if (string.IsNullOrEmpty(modName))
            {
                return BadRequest("Mod name is required");
            }

            // 清理mod名称，确保它是有效的文件名
            var cleanModName = CleanModName(modName);
            var filePath = Path.Combine(dataDirectory, $"{cleanModName}.txt");

            int usageCount = 0;

            // 检查文件是否存在
            if (System.IO.File.Exists(filePath))
            {
                // 读取现有计数
                var content = System.IO.File.ReadAllText(filePath);
                if (int.TryParse(content, out int existingCount))
                {
                    usageCount = existingCount;
                    _logger.Debug("返回模组 {ModName} 的使用次数: {UsageCount}", modName, usageCount);
                }
                else
                {
                    _logger.Warning("模组 {ModName} 的数据文件格式无效", modName);
                    return NotFound($"Invalid data format for mod: {modName}");
                }
            }
            else
            {
                _logger.Debug("模组 {ModName} 未找到", modName);
                return NotFound($"Mod not found: {modName}");
            }

            // 返回JSON格式的结果
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
            _logger.Debug("模组使用次数网页被访问");

            // 从查询参数获取语言偏好，默认为英文
            var lang = Request.Query.ContainsKey("lang") ? Request.Query["lang"].ToString() : "en";
            _logger.Debug("请求语言偏好: {Language}", lang);

            if (!Directory.Exists(dataDirectory))
            {
                _logger.Warning("模组统计数据目录不存在");
                return Ok(lang == "zh" ?
                    "<html><body><h1>模组使用统计</h1><p>暂无模组使用数据可用</p></body></html>" :
                    "<html><body><h1>Mod Usage Statistics</h1><p>No mod usage data available</p></body></html>");
            }

            var modFiles = Directory.GetFiles(dataDirectory, "*.txt");

            // 构建HTML响应
            var html = new System.Text.StringBuilder();

            // HTML头部和样式
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

            // 语言切换器
            html.AppendLine("        <div class=\"language-switcher\">");
            html.AppendLine("            <a href=\"?lang=en\"" + (lang == "en" ? " class=\"active\"" : "") + ">English</a>");
            html.AppendLine("            <a href=\"?lang=zh\"" + (lang == "zh" ? " class=\"active\"" : "") + ">中文</a>");
            html.AppendLine("        </div>");

            // 标题
            html.AppendLine("        <h1>" + (lang == "zh" ? "模组使用统计" : "Mod Usage Statistics") + "</h1>");

            // 统计摘要
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
                    _logger.Error(ex, "读取模组统计文件时出错: {FilePath}", file);
                }
            }

            html.AppendLine("        <div class=\"stats-summary\">");
            html.AppendLine("            <p>" + (lang == "zh" ?
                $"共追踪 {modFiles.Length} 个模组，总使用次数: {totalUses}" :
                $"Tracking {modFiles.Length} mods, Total uses: {totalUses}") + "</p>");
            html.AppendLine("        </div>");

            // 表格开始
            html.AppendLine("        <table>");
            html.AppendLine("            <thead>");
            html.AppendLine("                <tr>");
            html.AppendLine("                    <th>" + (lang == "zh" ? "模组名称" : "Mod Name") + "</th>");
            html.AppendLine("                    <th>" + (lang == "zh" ? "使用次数" : "Usage Count") + "</th>");
            html.AppendLine("                </tr>");
            html.AppendLine("            </thead>");
            html.AppendLine("            <tbody>");

            // 表格内容
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
                    _logger.Error(ex, "读取模组统计文件时出错: {FilePath}", file);
                    html.AppendLine("                <tr>");
                    html.AppendLine("                    <td colspan=\"2\" style=\"color: red;\">" +
                        (lang == "zh" ? "读取错误" : "Read Error") + ": " + file + "</td>");
                    html.AppendLine("                </tr>");
                }
            }

            // 表格结束
            html.AppendLine("            </tbody>");
            html.AppendLine("        </table>");

            // API使用示例
            html.AppendLine("        <div class=\"api-example\">");
            html.AppendLine("            <h2>" + (lang == "zh" ? "API 使用示例" : "API Usage Example") + "</h2>");
            html.AppendLine("            <p>" + (lang == "zh" ?
                "您可以使用以下API获取特定模组的使用次数:" :
                "You can use the following API to get usage count for a specific mod:") + "</p>");
            html.AppendLine("            <p><code>GET /api/modusage/usage/{modName}</code></p>");
            html.AppendLine("            <p>" + (lang == "zh" ?
                "示例响应:" : "Example response:") + "</p>");
            html.AppendLine("            <pre><code>{\n  \"modName\": \"TheOtherRoles\",\n  \"usageCount\": 42,\n  \"lastUpdated\": \"2023-06-15 14:30:25\"\n}</code></pre>");

            // 简单HTML示例
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
            // 记录原始模组名称
            var originalName = modName;

            // 移除或替换文件名中的非法字符
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                modName = modName.Replace(c, '_');
            }

            // 如果名称被修改，记录警告
            if (originalName != modName)
            {
                _logger.Warning("模组名称包含非法字符，已清理: {Original} -> {Cleaned}",
                    originalName, modName);
            }

            return modName;
        }
    }
}
