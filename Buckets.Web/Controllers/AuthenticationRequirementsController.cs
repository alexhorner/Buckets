using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Buckets.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthenticationRequirementsController
    {
        private readonly IConfiguration _configuration;

        public AuthenticationRequirementsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        /// <summary>
        /// Get a list of authentication requirements for authenticating operations
        /// </summary>
        [HttpGet]
        public Dictionary<string, bool> Get()
        {
            return _configuration.GetSection("AuthenticationRequirements").GetChildren().ToDictionary(x => x.Key, x => bool.Parse(x.Value));
        }
    }
}