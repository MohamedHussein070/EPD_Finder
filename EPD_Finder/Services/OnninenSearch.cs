//using HtmlAgilityPack;
//using System.Net;
//using System.Text.Json;

//namespace EPD_Finder.Services
//{
//    public class OnninenSearch
//    {
//        private readonly HttpClient _client;
//        private readonly ILogger<EpdService> _logger;
//        private readonly CookieContainer _cookies;
//        public OnninenSearch(HttpClient client, ILogger<EpdService> logger, CookieContainer cookies)
//        {
//            _client = client;
//            _logger = logger;
//            _cookies = cookies;
//        }
//        public async Task<string> TryGetEpdLink(string eNumber)
//        {
//            string productCode = await TryGetProductCode(eNumber);
//            if (string.IsNullOrEmpty(productCode))
//                return null;
//            string epdUrl = await TryGetEPDLinkFromProduct(productCode);
//            return epdUrl;
//        }
//        private async Task<string> TryGetProductCode(string eNumber)
//        {
//            if (string.IsNullOrWhiteSpace(eNumber))
//                throw new ArgumentException("E-nummer måste anges.", nameof(eNumber));

//            string quickSearchUrl = $"https://www.onninen.se/rest/v2/search/suggestions?term={eNumber}";
//            //för test
//            //var response = await _client.GetAsync(quickSearchUrl);
//            var response = await TryRequestWithFallback(_client, quickSearchUrl);
//            _logger.LogWarning("Onninen svarade {status}. Headers: {headers}",
//                response.StatusCode,
//                string.Join(" | ", response.Headers.Select(h => $"{h.Key}: {string.Join(",", h.Value)}")));

//            //ovan för testing
//            string json;
//            try
//            {
//                //json = await _client.GetStringAsync(quickSearchUrl);
//                response = await TryRequestWithFallback(_client, quickSearchUrl);
//                if (!response.IsSuccessStatusCode)
//                {
//                    _logger.LogWarning($"Onninen svarade {response.StatusCode} vid {quickSearchUrl}");
//                    return null;
//                }

//                json = await response.Content.ReadAsStringAsync();
//            }
//            catch (HttpRequestException ex)
//            {
//                _logger.LogError($"Fel vid hämtning av sökresultat: {ex.Message}");
//                return null;
//            }

//            using var doc = JsonDocument.Parse(json);
//            var root = doc.RootElement;
//            if (root.TryGetProperty("products", out var products))
//            {
//                var firstProduct = products.EnumerateArray().FirstOrDefault();
//                if (firstProduct.ValueKind != JsonValueKind.Undefined &&
//                    firstProduct.TryGetProperty("code", out var productCode))
//                {
//                    return productCode.GetString();
//                }
//            }

//            return null;
//        }

//        private async Task<string> TryGetEPDLinkFromProduct(string productCode)
//        {
//            if (string.IsNullOrWhiteSpace(productCode))
//                throw new ArgumentException("Produktkod måste anges.", nameof(productCode));


//            var cookieDump = _cookies.GetCookies(new Uri("https://www.onninen.se"));
//            _logger.LogWarning("Cookies: {cookies}", string.Join(" | ", cookieDump.Cast<Cookie>()));

//            string apiUrl = $"https://www.onninen.se/rest/v1/product/{productCode}";

//            try
//            {
//                //var json = await _client.GetStringAsync(apiUrl);
//                var response = await TryRequestWithFallback(_client, apiUrl);
//                if (!response.IsSuccessStatusCode)
//                {
//                    _logger.LogWarning($"Onninen svarade {response.StatusCode} vid {apiUrl}");
//                    return null;
//                }

//                var json = await response.Content.ReadAsStringAsync();

//                using var doc = JsonDocument.Parse(json);
//                var root = doc.RootElement;

//                // Leta efter dokumentfält
//                if (root.TryGetProperty("documents", out var docs))
//                {
//                    foreach (var docItem in docs.EnumerateArray())
//                    {
//                        if (docItem.TryGetProperty("documents", out var innerDocs))
//                        {
//                            foreach (var innerDoc in innerDocs.EnumerateArray())
//                            {
//                                if (innerDoc.TryGetProperty("name", out var name) &&
//                                    name.GetString()?.Contains("Miljövarudeklaration", StringComparison.OrdinalIgnoreCase) == true &&
//                                    innerDoc.TryGetProperty("link", out var link))
//                                {
//                                    return link.GetString();
//                                }
//                            }
//                        }
//                    }
//                }
//            }
//            catch (HttpRequestException ex)
//            {
//                _logger.LogError($"Fel vid hämtning från produkt-API: {ex.Message}");
//                return null;
//            }

//            _logger.LogError("Ingen EPD-länk hittades i produktens API-data.");
//            return null;
//        }
//        private async Task<HttpResponseMessage> TryRequestWithFallback(HttpClient client, string url)
//        {
//            // Första försök – vanlig request
//            var response = await client.GetAsync(url);
//            if (response.StatusCode != HttpStatusCode.Forbidden)
//                return response;

//            // Om 403 → försök igen med extra headers
//            var req = new HttpRequestMessage(HttpMethod.Get, url);
//            req.Headers.Referrer = new Uri("https://www.onninen.se/");
//            req.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
//            req.Headers.Add("Accept-Language", "sv-SE,sv;q=0.9,en;q=0.8");
//            req.Headers.Add("Cache-Control", "no-cache");

//            return await client.SendAsync(req);
//        }

//    }
//}
