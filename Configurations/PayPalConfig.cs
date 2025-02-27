using PayPal.Api;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace DoAnKy3.Configurations
{
    public class PayPalConfig
    {
        private readonly IConfiguration _config;

        public PayPalConfig(IConfiguration config)
        {
            _config = config;
        }

        public APIContext GetAPIContext()
        {
            string mode = _config["PayPal:Mode"];
            string clientId = _config["PayPal:ClientId"];
            string secret = _config["PayPal:Secret"];

            var config = new Dictionary<string, string>
        {
            { "mode", mode }
        };

            var accessToken = new OAuthTokenCredential(clientId, secret, config).GetAccessToken();
            return new APIContext(accessToken) { Config = config };
        }
    }
}
