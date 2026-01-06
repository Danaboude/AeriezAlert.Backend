using AeriezAlert.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeriezAlert.Backend.Controllers;

[ApiController]
[Route("api/notify")]
public class NotificationController : ControllerBase
{
    private readonly MqttService _mqttService;

    public NotificationController(MqttService mqttService)
    {
        _mqttService = mqttService;
    }

    [HttpPost]
    public async Task<IActionResult> SendNotification([FromBody] NotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Topic) || request.Message == null)
        {
            return BadRequest("Topic and Message are required.");
        }

        await _mqttService.PublishAsync(request.Topic, request.Message);
        return Ok(new { Message = "Notification queued" });
    }
}

public class NotificationRequest
{
    public string Topic { get; set; } = string.Empty;
    // Flexible message payload
    public object Message { get; set; } = new();
}
