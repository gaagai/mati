using CsvHelper;
using CsvHelper.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SuperPharmScraper.Models;
using System.Globalization;
using System.Text;

namespace SuperPharmScraper.Services
{
    public class SuperPharmScraperService
    {
        public List<BranchData> ScrapeBranchesFromList(string url)
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--no-sandbox");

            var branches = new List<BranchData>();

            using (var driver = new ChromeDriver(options))
            {
                try
                {
                    driver.Navigate().GoToUrl(url);
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));

                    // Load all branches
                    LoadAllBranches(driver, wait);

                    // Collect all branch links and basic info
                    var storeLinks = CollectStoreLinks(driver);

                    // Process each branch
                    foreach (var storeInfo in storeLinks)
                    {
                        try
                        {
                            var branchData = ProcessBranchDetails(driver, wait, storeInfo);
                            branches.Add(branchData);

                            // Add a small delay to avoid overwhelming the server
                            Thread.Sleep(1000);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing branch {storeInfo.name}: {ex.Message}");
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during scraping: {ex.Message}");
                    throw;
                }
            }

            return branches;
        }

        private List<(string name, string address, string city, string link)> CollectStoreLinks(IWebDriver driver)
        {
            var storeLinks = new List<(string name, string address, string city, string link)>();

            var storeElements = driver.FindElements(By.CssSelector("li.store"));
            Console.WriteLine($"Found {storeElements.Count} branches on the main page.");

            foreach (var store in storeElements)
            {
                try
                {
                    var name = store.FindElement(By.CssSelector("h5.store-name")).Text.Trim();
                    var address = store.FindElement(By.CssSelector("p.store-address")).Text.Trim();
                    var city = address.Contains(",") ? address.Split(',')[1].Trim() : "Unknown";
                    var link = store.FindElement(By.CssSelector("a")).GetDomAttribute("href");

                    storeLinks.Add((name, address, city, link));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting store info: {ex.Message}");
                }
            }

            return storeLinks;
        }

        private BranchData ProcessBranchDetails(IWebDriver driver, WebDriverWait wait, (string name, string address, string city, string link) storeInfo)
        {
            driver.Navigate().GoToUrl(storeInfo.link);
            wait.Until(d => d.FindElement(By.CssSelector(".branch-contact-and-hours-details")));
            Thread.Sleep(1000);

            string latitude = "0.0";
            string longitude = "0.0";

            try
            {
                var distanceElement = wait.Until(d => d.FindElement(By.CssSelector("div[data-latitude]")));
                latitude = distanceElement.GetDomAttribute("data-latitude") ?? "0.0";
                longitude = distanceElement.GetDomAttribute("data-longitude") ?? "0.0";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not get coordinates for store {storeInfo.name}: {ex.Message}");
            }

            string hours = "Not Available";
            try
            {
                var hoursElement = driver.FindElement(By.CssSelector(".wrapper-opening-hours"));
                if (hoursElement != null)
                {
                    hours = hoursElement.Text.Trim();
                }
            }
            catch
            {
                Console.WriteLine($"No detailed hours found for {storeInfo.name}");
            }

            return new BranchData
            {
                Name = storeInfo.name,
                Address = storeInfo.address,
                City = storeInfo.city,
                Hours = hours,
                Latitude = latitude,
                Longitude = longitude,
                Is24Hours = hours.Contains("00:00") ? "Yes" : "No"
            };
        }

        private void LoadAllBranches(IWebDriver driver, WebDriverWait wait)
        {
            const int maxRetries = 5;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    var loadMoreButton = driver.FindElements(By.CssSelector(".btn-more")).FirstOrDefault();
                    if (loadMoreButton == null || !loadMoreButton.Displayed)
                    {
                        Console.WriteLine("No more 'Load More' buttons found.");
                        break;
                    }

                    Console.WriteLine("Clicking 'Load More' button...");
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", loadMoreButton);
                    Thread.Sleep(1000);
                    loadMoreButton.Click();
                    Thread.Sleep(2000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading more branches: {ex.Message}");
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        Console.WriteLine("Max retries reached for 'Load More' button.");
                        break;
                    }
                }
            }
        }

        public void SaveToCsv(List<BranchData> branches, string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ",",
                    HasHeaderRecord = true,
                    Encoding = Encoding.UTF8
                };

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                using (var csv = new CsvWriter(writer, config))
                {
                    csv.WriteHeader<BranchData>();
                    csv.NextRecord();

                    foreach (var branch in branches)
                    {
                        csv.WriteRecord(branch);
                        csv.NextRecord();
                    }
                }

                Console.WriteLine($"Successfully saved {branches.Count} branches to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save CSV file to {filePath}: {ex.Message}");
                throw;
            }
        }

        public string SaveToCsvWithTimestamp(List<BranchData> branches, string baseDirectory)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"super_pharm_branches_{timestamp}.csv";
            var filePath = Path.Combine(baseDirectory, fileName);

            SaveToCsv(branches, filePath);
            return filePath;
        }
    }
}
