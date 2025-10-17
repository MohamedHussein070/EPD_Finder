using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Vml;
using EPD_Finder.Models;
using EPD_Finder.Services.IServices;
using System.Net;
using System.Text.RegularExpressions;

namespace EPD_Finder.Services
{
    public class EpdService : IEpdService
    {
        private readonly HttpClient _client;
        private readonly ILogger<EpdService> _logger;
        private readonly AhlsellSearch _ahlsell;
        private readonly EnummersokSearch _enummersok;
        private readonly SolarSearch _solar;
        private readonly SoneparSearch _sonepar;
        private readonly RexelSearch _rexel;
        //private readonly OnninenSearch _onninen;
        private readonly Schneider _schneider;

        public EpdService(HttpClient client,
            ILogger<EpdService> logger,
            AhlsellSearch ahlsell,
            EnummersokSearch enummersok,
            SolarSearch solar,
            SoneparSearch sonepar,
            RexelSearch rexel,
            //OnninenSearch onninen,
            Schneider schneider
            )
        {
            _client = client;
            _logger = logger;
            _ahlsell = ahlsell;
            _enummersok = enummersok;
            _solar = solar;
            _sonepar = sonepar;
            _rexel = rexel;
            //_onninen = onninen;
            _schneider = schneider;
        }
        public List<string> ParseInput(string eNumbers, IFormFile file)
        {
            var list = new List<string>();

            // Från textarea
            if (!string.IsNullOrWhiteSpace(eNumbers))
            {
                list.AddRange(eNumbers
                    .Split(new[] { ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()));
            }

            // Från fil (Excel eller CSV)
            if (file != null && file.Length > 0)
            {
                using var stream = file.OpenReadStream();
                if (file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(stream);
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine()?.Trim();
                        if (string.IsNullOrWhiteSpace(line)) continue; //skip if null or empty
                        if (!line.Any(char.IsDigit)) continue; // Skip lines without digits
                        list.Add(line);
                    }
                }
                else if (file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
                    var ws = workbook.Worksheets.First();
                    foreach (var row in ws.RowsUsed())
                    {
                        var val = row.Cell(1).GetValue<string>().Trim();
                        if (string.IsNullOrWhiteSpace(val)) continue; //skip if null or empty
                        if (!val.Any(char.IsDigit)) continue; // Skip rows without digits
                        list.Add(val);
                    }
                }
            }

            return list.Distinct().ToList();
        }

        public async Task<ArticleResult> TryGetEpdLink(string eNumber, List<string> selectedSources)
        {
            var tasks = new List<Task<ArticleResult>>();

            if (selectedSources.Contains("E-nummersök"))
                tasks.Add(TryGetSourceLink(eNumber, "E-nummersök", _enummersok));

            if (selectedSources.Contains("Ahlsell"))
                tasks.Add(TryGetSourceLink(eNumber, "Ahlsell", _ahlsell));

            if (selectedSources.Contains("Solar"))
                tasks.Add(TryGetSourceLink(eNumber, "Solar", _solar));

            if (selectedSources.Contains("Sonepar"))
                tasks.Add(TryGetSourceLink(eNumber, "Sonepar", _sonepar));

            if (selectedSources.Contains("Rexel"))
                tasks.Add(TryGetSourceLink(eNumber, "Rexel", _rexel));

            //if (selectedSources.Contains("Onninen"))
            //    tasks.Add(TryGetSourceLink(eNumber, "Onninen", _onninen));

            var results = await Task.WhenAll(tasks);

            // Returnera första som lyckas
            var firstValid = results.FirstOrDefault(r => r != null);
            if (firstValid != null)
                return firstValid;

            throw new ArgumentException("Ej hittad");
        }
        private async Task<ArticleResult> TryGetSourceLink(string eNumber, string sourceName, dynamic sourceService)
        {
            try
            {
                var pdfUrl = await sourceService.TryGetEpdLink(eNumber);
                if (!string.IsNullOrEmpty(pdfUrl) && await IsLinkValid(pdfUrl))
                    return new ArticleResult
                    {
                        ENumber = eNumber,
                        Source = sourceName,
                        EpdLink = pdfUrl
                    };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching EPD for {eNumber} ,{ex}");
            }

            return null;
        }

        private async Task<bool> IsLinkValid(string url)
        {
            try
            {
                if(url.ToLower().Contains("schneider"))
                {
                    return await _schneider.TryVerifyLink(url); //special case
                }

                // First try HEAD request
                var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                var headResponse = await _client.SendAsync(headRequest);
                if (headResponse.IsSuccessStatusCode) return true;
                else _logger.LogWarning($"Response code on failed Head requests: {headResponse.StatusCode}");

                // Fallback to GET but only read headers
                var getResponse = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                if (getResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Response code APPROVED on Get requests: {getResponse}");
                    return true;
                }
                else
                {
                    if (getResponse.StatusCode.ToString().Contains("404"))
                    {
                        _logger.LogWarning($"Response status code:: {getResponse.StatusCode.ToString()}");
                        _logger.LogWarning($"Response code FAILED on Get requests: {getResponse}");
                        return false;
                    }
                    else if (getResponse.StatusCode.ToString().Contains("403"))
                    {
                        var urlMatch = Regex.Match(url, @"https?://\S+?\.pdf(?=(\?|\s|$))", RegexOptions.IgnoreCase);
                        if (urlMatch.Success)
                        {
                            return true;
                        }
                    }
                    _logger.LogWarning($"Response status code:: {getResponse.StatusCode.ToString()}");
                    _logger.LogWarning($"Response code FAILED on Get requests: {getResponse}");
                    return false;
                }
            }
            catch (Exception ex) 
            {
                _logger.LogWarning($"Response code FAILED with Exception: {ex}");
                return false;
            }
        }
    }
}
