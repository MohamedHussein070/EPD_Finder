using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace EPD_Finder.Services
{
    public class SoneparSearch
    {
        private readonly HttpClient _client;
        private readonly ILogger<EpdService> _logger;
        public SoneparSearch(HttpClient client, ILogger<EpdService> logger)
        {
            _client = client;
            _logger = logger; 
        }
        public async Task<string> TryGetEpdLink(string eNumber)
        {
            string productUrl = $"https://www.elektroskandia.se/produkt/hamtamodel?artnr={eNumber}";
            string epdUrl = await TryGetEPDLinkFromProductPage(productUrl);
            return epdUrl;
        }
        private async Task<string> TryGetEPDLinkFromProductPage(string productUrl)
        {
            if (string.IsNullOrWhiteSpace(productUrl))
                throw new ArgumentException("Produkt Url saknas", nameof(productUrl));

            string json;
            try
            {
                json = await _client.GetStringAsync(productUrl);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Fel vid hämtning av sökresultat: {ex.Message}");
                return null;
            }
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("Artikeln", out var artikel))
                return null;

            if (!artikel.TryGetProperty("ArtikelBildernaPIM", out var bilder))
                return null;

            foreach (var bild in bilder.EnumerateArray())
            {
                if (bild.TryGetProperty("Forklaring", out var forklaring) &&
                    forklaring.GetString() == "Livscykelanalys (EPD/PEP)" &&
                    bild.TryGetProperty("Bild", out var url))
                {
                    return url.GetString();
                }
            }

            return null;
        }
    }
}
