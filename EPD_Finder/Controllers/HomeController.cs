using ClosedXML.Excel;
using EPD_Finder.Models;
using EPD_Finder.Services.IServices;
using EPD_Finder.Utility;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace EPD_Finder.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IEpdService _epdService;
        private static ConcurrentDictionary<string, JobData> _jobs = new();
        private static readonly SemaphoreSlim _limit = new SemaphoreSlim(10);
        public HomeController(ILogger<HomeController> logger, IEpdService epdService)
        {
            _logger = logger;
            _epdService = epdService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateJob(IFormFile file, string eNumbers, [FromForm] List<string> sources)
        {
            // Ignorera det som inte är synligt
            if (!string.IsNullOrWhiteSpace(eNumbers) && file != null && file.Length > 0)
            {
                // Om båda är ifyllda, använd bara det synliga (t.ex. textfält)
                // Antag att frontend markerar vilket som är aktivt
                // Här kan du kontrollera t.ex. en extra "inputType" parameter
                var activeInput = Request.Form["inputType"];
                if (activeInput == "text")
                    file = null;
                else
                    eNumbers = null;
            }
            var list = _epdService.ParseInput(eNumbers, file);
            if (!list.Any()) return BadRequest("Inga E-nummer hittades.");

            var jobId = Guid.NewGuid().ToString();
            _jobs[jobId] = new JobData
            {
                ENumbers = list,
                Sources = sources ?? new List<string>()
            };

            return Ok(new { jobId, eNumbers = list });
        }

        [HttpGet]
        public async Task GetResultsStream(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId) || !_jobs.TryGetValue(jobId, out var jobData))
            {
                Response.StatusCode = 400; // Bad Request
                await Response.WriteAsync("Invalid or missing jobId");
                return;
            }

            Response.ContentType = "text/event-stream";
            Response.Headers.Add("Cache-Control", "no-cache");

            var tasks = jobData.ENumbers.Select(async num =>
            {
                await _limit.WaitAsync();
                try
                {
                    ArticleResult result = await _epdService.TryGetEpdLink(num, jobData.Sources);
                    return result;
                }
                catch
                {
                    return new ArticleResult { ENumber = num, Source="", EpdLink = "Ej hittad" };
                }
                finally
                {
                    _limit.Release();
                }
            }).ToList();

            while (tasks.Any())
            {
                var finished = await Task.WhenAny(tasks);
                tasks.Remove(finished);

                var result = await finished;
                var json = System.Text.Json.JsonSerializer.Serialize(result);
                await Response.WriteAsync($"data: {json}\n\n");
                await Response.Body.FlushAsync();
            }

            await Response.WriteAsync("event: done\ndata: complete\n\n");
            await Response.Body.FlushAsync();

            _jobs.TryRemove(jobId, out _);
        }



        [HttpPost]
        public IActionResult DownloadExcel([FromBody] List<ArticleResult> list)
        {

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("EPD Links");
            ws.Cell(1, 1).Value = "E-nummer";
            ws.Cell(1, 2).Value = "EPD-länk";
            ws.Range(1, 1, 1, 3).Style.Font.Bold = true;

            for (int i = 0; i < list.Count; i++)
            {
                ws.Cell(i + 2, 1).Value = list[i].ENumber;
                var cell = ws.Cell(i + 2, 2);
                cell.Value = list[i].EpdLink;
                if (!string.IsNullOrWhiteSpace(list[i].EpdLink) && list[i].EpdLink.StartsWith("http"))
                {
                    cell.SetHyperlink(new XLHyperlink(list[i].EpdLink));
                    cell.Style.Font.FontColor = XLColor.Blue;
                    cell.Style.Font.Underline = XLFontUnderlineValues.Single;
                }
                else
                {
                    cell.Value = list[i].EpdLink ?? "Ej hittad";
                }
            }
            ws.Columns().AdjustToContents();
            foreach (var col in ws.Columns())
            {
                if (col.Width < 15) col.Width = 15;
            }
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "epd_links.xlsx");
        }

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult License()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
