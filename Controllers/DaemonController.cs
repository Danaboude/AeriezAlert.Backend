using AeriezAlert.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeriezAlert.Backend.Controllers;

[ApiController]
[Route("api/daemon")]
public class DaemonController : ControllerBase
{
    private readonly DaemonService _daemonService;

    public DaemonController(DaemonService daemonService)
    {
        _daemonService = daemonService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new { IsRunning = _daemonService.IsRunning });
    }

    [HttpPost("start")]
    public IActionResult Start()
    {
        _daemonService.StartPolling();
        return Ok(new { Message = "Daemon started" });
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        _daemonService.StopPolling();
        return Ok(new { Message = "Daemon stopped" });
    }
}
