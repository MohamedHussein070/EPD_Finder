using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EPD_Finder.Services
{
    public class RexelSearch
    {
        private readonly HttpClient _client;
        private readonly ILogger<EpdService> _logger;
        public RexelSearch(HttpClient client, ILogger<EpdService> logger)
        {
            _client = client;
            _logger = logger;
        }
        public async Task<string> TryGetEpdLink(string eNumber)
        {
            string epdUrl = await TryGetEPDLinkFromProductPage(eNumber);
            return epdUrl;
        }
        private async Task<string> TryGetEPDLinkFromProductPage(string eNumber)
        {
            var url = "https://eu.dif.rexel.com/web/api/v1/dam/products/assets";

            var payload = new
            {
                skus = new[] { eNumber }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("x-banner", "swe");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                using var response = await _client.SendAsync(request);

                var jsonString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonString);

                foreach (var product in doc.RootElement.EnumerateArray())
                {
                    if (!product.TryGetProperty("assets", out var assets))
                        continue;

                    foreach (var asset in assets.EnumerateArray())
                    {
                        if (asset.TryGetProperty("mime", out var mime) &&
                            (mime.GetString() == "application/pdf" || mime.GetString() == "text/html") &&
                            asset.TryGetProperty("MIME_PURPOSE", out var purpose) &&
                            purpose.GetString() != null &&
                            purpose.GetString().Contains("Miljövarudeklaration", StringComparison.OrdinalIgnoreCase) &&
                            asset.TryGetProperty("url", out var urls) &&
                            urls.GetArrayLength() > 0)
                        {

                            return urls[0].GetProperty("url").GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching EPD for {eNumber}");
            }

            return null;
        }
    }
}
