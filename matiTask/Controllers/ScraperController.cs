using Microsoft.AspNetCore.Mvc;
using SuperPharmScraper.Services;
using System;
using System.IO;

namespace SuperPharmScraper.Controllers
{
    [ApiController]
    [Route("api/scraper")] // Base route for this controller
    public class ScraperController : ControllerBase
    {
        private readonly SuperPharmScraperService _scraper;

        public ScraperController()
        {
            // Instantiate the service
            _scraper = new SuperPharmScraperService();
        }

        /// <summary>
        /// Scrapes data from the Super-Pharm branches page.
        /// </summary>
        /// <returns>JSON with the file path and message</returns>
        [HttpGet("scrape")]
        public IActionResult ScrapeData()
        {
            try
            {
                // Target URL
                var url = "https://shop.super-pharm.co.il/branches";
                Console.WriteLine($"Starting scrape for URL: {url}");

                // Perform scraping
                var branches = _scraper.ScrapeBranchesFromList(url);
                Console.WriteLine($"Scraped {branches.Count} branches successfully.");

                // Define directory and save CSV
                var baseDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
                Directory.CreateDirectory(baseDirectory); // Ensure directory exists
                Console.WriteLine($"Directory for CSV: {baseDirectory}");

                var filePath = _scraper.SaveToCsvWithTimestamp(branches, baseDirectory);
                Console.WriteLine($"File saved successfully at: {filePath}");

                // Return success response
                return Ok(new
                {
                    message = "נתונים נשמרו בהצלחה!",
                    filePath,
                    directory = baseDirectory
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Unauthorized Access: {ex.Message}");
                return StatusCode(500, new
                {
                    message = "שגיאה בהרשאות גישה. וודא שלשרת יש הרשאות כתיבה.",
                    error = ex.Message
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during scraping or saving: {ex.Message}");
                return StatusCode(500, new
                {
                    message = "אירעה שגיאה בעת גרידת הנתונים או שמירת הקובץ.",
                    error = ex.Message
                });
            }
        }
    }
}
