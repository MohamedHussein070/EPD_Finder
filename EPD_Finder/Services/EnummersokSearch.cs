using System.Text.Json;
using System.Text.RegularExpressions;

namespace EPD_Finder.Services
{
    public class EnummersokSearch
    {
        private readonly HttpClient _client;
        private readonly ILogger<EpdService> _logger;
        private const string BaseUrl = "https://www.e-nummersok.se/infoDocs/EPD/";
        public EnummersokSearch(HttpClient client, ILogger<EpdService> logger)
        {
            _client = client;
            _logger = logger;
        }
        public async Task<string> TryGetEpdLink(string eNumber)
        {
            var url = await GetEnummersokUrl(eNumber);
            var supplierID = await GetSupplierIdFromEnumberWithAsync(url, eNumber);
            string pdfUrl = "";
            if (supplierID != null)
            {
                pdfUrl = $"{BaseUrl}EPD_{supplierID}_{eNumber}.pdf";
                return pdfUrl;
            }
            else
            {
                pdfUrl = $"{BaseUrl}EPD_{eNumber}.pdf";
                return pdfUrl;
            } 
        }
        private async Task<string> GetEnummersokUrl(string eNumber)
        {
            if (string.IsNullOrWhiteSpace(eNumber))
                return null;

            try
            {
                string apiUrl = $"https://www.e-nummersok.se/ApiSearch/Suggest?ActiveOnly=true&PicsOnly=true&Query={eNumber}&Sort=1";
                var response = await _client.GetStringAsync(apiUrl);

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.GetProperty("Status").GetString() != "OK")
                    return null;

                var suggestions = root.GetProperty("Data").GetProperty("ProductSuggestionRows");
                if (suggestions.GetArrayLength() == 0)
                    return null;

                var firstProduct = suggestions[0];
                string url = firstProduct.GetProperty("Url").GetString()!;
                return url;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fel vid hämtning av URL för e-nummer {Enummer}", eNumber);
                return null;
            }
        }

        private async Task<string?> GetSupplierIdFromEnumberWithAsync(string url, string eNumber)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                // Plocka ut sista numret i urlen med regex
                var match = Regex.Match(url, @"-(\d+)$");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fel vid hämtning av sista numret från produkt-URL för e-nummer {Enummer}", eNumber);
                return null;
            }
        }
    }
}
