using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using gRPCMessage.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessageController : ControllerBase
    {
        public MessageController()
        {
        }

        [HttpGet]
        public async Task<IActionResult> SendMessage([FromQuery] string data)
        {
            await FanoutMessageService.FanoutAsync(new MessageReply() { Data = data });
            return Ok("send out");
        }
    }
}
