using Microsoft.AspNetCore.Mvc;
using Impostor.Server.Service;
using System;

namespace Impostor.Server.Http
{
    [ApiController]
    [Route("api/debug")]
    public class DebugController : ControllerBase
    {
        [HttpGet("auth-cache")]
        public IActionResult GetAuthCacheStatus()
        {
            var stats = AuthCacheService.GetCacheStats();

            return Ok(new
            {
                PuidCount = stats.PuidCount,
                IpMappingCount = stats.IpMappingCount,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
