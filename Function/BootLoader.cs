using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net.Http;
using System.Security.Claims;

namespace Company.Function
{
    public static class BootLoader
    {
        [FunctionName("BootLoader")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
            Route = "{requestedRoute}")] HttpRequestMessage req,
            string requestedRoute,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            requestedRoute = requestedRoute.ToLower();
            switch (requestedRoute)
            {
                case "anonymous":
                    return Anonymous(req, log);
                case "authenticated":
                    return await Authenticated(req, log);
                default:
                    break;
            }
            return (ActionResult)new OkObjectResult(requestedRoute);
        }

        private static IActionResult Anonymous(HttpRequestMessage req, ILogger log)
        {
            return (ActionResult)new OkObjectResult("anonymous");
        }

        private static async Task<IActionResult> Authenticated(HttpRequestMessage req, ILogger log)
        {
            var accessToken = GetAccessToken(req);
            var claimsPrincipal = await ValidateAccessToken(accessToken, log);
            if (claimsPrincipal != null)
            {
                return (ActionResult)new OkObjectResult(claimsPrincipal.Identity.Name);
            }
            else
            {
                return (ActionResult)new UnauthorizedResult();
            }            
        }

        private static string GetAccessToken(HttpRequestMessage req)
        {
            var scheme = req.Headers.Authorization?.Scheme;
            if(scheme != null && scheme.Equals("Bearer"))
                return req.Headers.Authorization.Parameter;
            return null;
        }

        private static async Task<ClaimsPrincipal> ValidateAccessToken(string accessToken, ILogger log)
        {
            var audience = Constants.audience;
            var clientID = Constants.clientID;
            var tenant = Constants.tenant;
            var tenantid = Constants.tenantid;
            var aadInstance = Constants.aadInstance;
            var authority = Constants.authority;
            var validIssuers = Constants.validIssuers;

            // Debugging purposes only, set this to false for production
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;

            ConfigurationManager<OpenIdConnectConfiguration> configManager =
                new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{authority}/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever());

            OpenIdConnectConfiguration config = null;
            config = await configManager.GetConfigurationAsync();

            ISecurityTokenValidator tokenValidator = new JwtSecurityTokenHandler();

            // Initialize the token validation parameters
            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                // App Id URI and AppId of this service application are both valid audiences.
                ValidAudiences = new[] { audience, clientID },

                // Support Azure AD V1 and V2 endpoints.
                ValidIssuers = validIssuers,
                IssuerSigningKeys = config.SigningKeys
            };

            try
            {
                SecurityToken securityToken;
                var claimsPrincipal = tokenValidator.ValidateToken(accessToken, validationParameters, out securityToken);
                return claimsPrincipal;
            }
            catch (Exception ex)
            {
                log.LogError(ex.ToString());
            }
            return null;
        }
    }
}

