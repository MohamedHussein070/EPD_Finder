using HtmlAgilityPack;
using System.Net;
using System.Text;
using System.Text.Json;

namespace EPD_Finder.Services
{
    public class SolarSearch
    {
        private readonly HttpClient _client;
        private readonly ILogger<EpdService> _logger;
        public SolarSearch(HttpClient client, ILogger<EpdService> logger)
        {
            _client = client;
            _logger = logger;
        }
        public async Task<string> TryGetEpdLink(string eNumber)
        {
            string productUrl = await TryGetProductUrl(eNumber);
            if (string.IsNullOrEmpty(productUrl))
                return null;
            string epdUrl = await TryGetEPDLinkFromProductPage(productUrl);
            return epdUrl;
        }
        private async Task<string> TryGetProductUrl(string eNumber)
        {
            if (string.IsNullOrWhiteSpace(eNumber))
                throw new ArgumentException("E-nummer måste anges.", nameof(eNumber));

            var apiUrl = "https://www.solar.se/api/search/basicCommerceSearch";

            var requestBody = new
            {
                query = eNumber,
                page = 0,
                pageSize = 24
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _client.PostAsync(apiUrl, jsonContent);
            if (!response.IsSuccessStatusCode)
                return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var product = root.GetProperty("Documents")
                              .EnumerateArray()
                              .FirstOrDefault(p => p.GetProperty("ItemArticleDisplayNumber").GetString() == eNumber);

            if (product.ValueKind != JsonValueKind.Undefined)
            {
                string url = product.GetProperty("Url").GetString();
                return "https://www.solar.se" + url;
            }

            return null;
        }

        private async Task<string> TryGetEPDLinkFromProductPage(string productUrl)
        {
            if (string.IsNullOrWhiteSpace(productUrl))
                throw new ArgumentException("Produkt-URL måste anges.", nameof(productUrl));

            string productHtml;
            try
            {
                productHtml = await _client.GetStringAsync(productUrl);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Fel vid hämtning av produktsidan: {ex.Message}");
                return null;
            }

            var productDoc = new HtmlDocument();
            productDoc.LoadHtml(productHtml);

            var containers = productDoc.DocumentNode.SelectNodes("//solar-product-download-container");

            string epdUrl = null;

            foreach (var container in containers)
            {
                var linkAttr = container.GetAttributeValue("link", null);
                if (linkAttr == null) continue;

                linkAttr = WebUtility.HtmlDecode(linkAttr);

                using var jsonDoc = JsonDocument.Parse(linkAttr);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("displayName", out var displayNameProp) &&
                    displayNameProp.GetString().Contains("EPD", StringComparison.OrdinalIgnoreCase) || displayNameProp.GetString().Contains("miljöprofil", StringComparison.OrdinalIgnoreCase))
                {
                    epdUrl = root.GetProperty("url").GetString();
                    break; 
                }
            }

            if (epdUrl != null)
            {
                return epdUrl;
            }
            _logger.LogError("Ingen EPD-länk hittades på produktsidan.");
            return null;
        }
    }
}
