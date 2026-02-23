using AeriezAlert.Backend.Models;
using AeriezAlert.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeriezAlert.Backend.Controllers
{
    [ApiController]
    [Route("api/connection")]
    public class ConnectionController : ControllerBase
    {
        private readonly PhoneNotificationService _phoneNotificationService;

        public ConnectionController(PhoneNotificationService phoneNotificationService)
        {
            _phoneNotificationService = phoneNotificationService;
        }



        [HttpPost("notifications")]
        public ActionResult<PhoneWithNotificationResult> GetNotifications([FromBody] List<PhonesPings> phones)
        {
            if (phones == null || !phones.Any())
            {
                return BadRequest("List of phones is required.");
            }

            var result = _phoneNotificationService.GetNotificationsGlobal(phones);
            return Ok(result);
        }
    }
}
