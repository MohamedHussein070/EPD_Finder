using System.Net;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;

namespace EPD_Finder.Services
{
    public class Schneider
    {
        private readonly HttpClient _client;
        private readonly ILogger<EpdService> _logger;
        private readonly string BaseUrl = "https://www.se.com/se/sv/download/document/";
        public Schneider(HttpClient client, ILogger<EpdService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<bool> TryVerifyLink(string pdfUrl)
        {
            // Extract the document reference
            var match = Regex.Match(pdfUrl, @"p_Doc_Ref=([A-Za-z0-9_-]+)");
            if (!match.Success)
            {
                _logger.LogWarning("Ingen giltig p_Doc_Ref hittades i URL: {url}", pdfUrl);
                return false;
            }
            string docRef = match.Groups[1].Value;
            string checkUrl = $"{BaseUrl}{docRef}/";

            try
            {
                var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, checkUrl));
                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fel vid verifiering av Schneider-länk: {url}", pdfUrl);
                return false;
            }
        }
    }
}
