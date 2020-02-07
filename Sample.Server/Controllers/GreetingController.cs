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
    public class GreetingController : ControllerBase
    {
        public GreetingController()
        {
        }

        [HttpGet]
        public async Task<IActionResult> GetGreetingAsync()
        {
            return Ok("use/message?data={data} to fanout");
        }
    }
}
