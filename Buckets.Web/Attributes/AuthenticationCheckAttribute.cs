using System;
using System.Linq;
using Buckets.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Buckets.Web.Attributes
{
    public class AuthenticationCheckAttribute : ActionFilterAttribute
    {
        private readonly string? _configurationCheck;

        public AuthenticationCheckAttribute(string? configurationCheck = null)
        {
            _configurationCheck = configurationCheck;
        }
        
        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            IConfiguration? configuration = ctx.HttpContext.RequestServices.GetService<IConfiguration>();
            
            if (configuration == null) throw new Exception("Unable to resolve service " + nameof(IConfiguration));
            
            if (_configurationCheck != null && !configuration.GetValue<bool>($"AuthenticationRequirements:{_configurationCheck}")) return;
            
            //Check header provided
            if (!ctx.HttpContext.Request.Headers.TryGetValue("Authorization", out StringValues auth))
            {
                ctx.Result = new JsonResult(new BasicResponse
                {
                    Message = "No Authorization header provided",
                    Error = true
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };

                return;
            }
            
            //Check header format
            if(!auth[0].StartsWith("Bearer "))
            {
                ctx.Result = new JsonResult(new BasicResponse
                {
                    Message = "Authorization header is not a bearer token",
                    Error = true
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };

                return;
            }
            
            //Extract token and fetch application
            string token = auth[0].Split(" ", 2)[1];

            if (!configuration.GetSection("AuthenticationKeys").Get<string[]>().Contains(token))
            {
                ctx.Result = new JsonResult(new BasicResponse
                {
                    Message = "Authorization header token is invalid",
                    Error = true
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };

                return;
            }
        }
    }
}