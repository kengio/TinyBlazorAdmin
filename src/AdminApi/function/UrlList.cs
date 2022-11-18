/*
```c#
Input:


Output:
    {
        "Url": "https://SOME_URL",
        "Clicks": 0,
        "PartitionKey": "d",
        "title": "Quickstart: Create your first function in Azure using Visual Studio"
        "RowKey": "doc",
        "Timestamp": "0001-01-01T00:00:00+00:00",
        "ETag": "W/\"datetime'2020-05-06T14%3A33%3A51.2639969Z'\""
    }
*/

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Linq;

using Cloud5mins.AzShortener;
using Cloud5mins.domain;
using System.Security.Claims;


//using Microsoft.AspNetCore.Http;

namespace Cloud5mins.Function
{
    public class UrlList
    {

        private readonly ILogger _logger;
        private readonly AdminApiSettings _adminApiSettings;

        public UrlList(ILoggerFactory loggerFactory, AdminApiSettings settings)
        {
            _logger = loggerFactory.CreateLogger<UrlList>();
            _adminApiSettings = settings;
        }

        [Function("UrlList")]
        public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequestData req, ExecutionContext context)
        {
            _logger.LogInformation($"C# HTTP trigger function processed this request: {req}");

            var result = new ListResponse();
            string userId = string.Empty;
            

            StorageTableHelper stgHelper = new StorageTableHelper(_adminApiSettings.UlsDataStorage);

            try
            {
                var principal = StaticWebAppsAuth.GetClaimsPrincipal(req);
                // var invalidRequest = ClaimsUtility.CatchUnauthorize(principal, _logger);
                // if (invalidRequest != null)
                // {
                //     return req.CreateResponse(HttpStatusCode.Unauthorized);;
                // }
                // else
                // {
                //     userId = principal.FindFirst(ClaimTypes.GivenName).Value;
                //     _logger.LogInformation("Authenticated user {user}.", userId);
                // }
                userId = principal.FindFirst(ClaimTypes.NameIdentifier).Value;
                if(String.IsNullOrEmpty(userId)){
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }
                _logger.LogInformation("Authenticated user {user}.", userId);

                result.UrlList = await stgHelper.GetAllShortUrlEntities();
                result.UrlList = result.UrlList.Where(p => !(p.IsArchived ?? false)).ToList();
                var host = string.IsNullOrEmpty(_adminApiSettings.customDomain) ? req.Url.Host: _adminApiSettings.customDomain;
                foreach (ShortUrlEntity url in result.UrlList)
                {
                    url.ShortUrl = Utility.GetShortUrl(host, url.RowKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error was encountered.");
                var badres = req.CreateResponse(HttpStatusCode.BadRequest);
                await badres.WriteAsJsonAsync(new {Message = ex.Message });
                return badres;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);

            return response;
        }
    }
}
