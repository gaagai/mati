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

                    // Collect store details
                    var storeLinks = CollectStoreLinks(driver);

                    // Process each store link
                    foreach (var storeInfo in storeLinks)
                    {
                        try
                        {
                            var branchData = ProcessBranchDetails(driver, wait, storeInfo);
                            branches.Add(branchData);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing branch {storeInfo.name}: {ex.Message}");
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

        private List<(string name, string address, string status, string is24Hours, string city, string hours, string link)> CollectStoreLinks(IWebDriver driver)
        {
            var storeLinks = new List<(string name, string address, string status, string is24Hours, string city, string hours, string link)>();
            var storeElements = driver.FindElements(By.CssSelector("li.store"));

            foreach (var store in storeElements)
            {
                try
                {
                    var name = store.FindElement(By.CssSelector("h5.store-name")).Text.Trim();
                    var address = store.FindElement(By.CssSelector("p.store-address")).Text.Trim();
                    var status = store.FindElements(By.CssSelector(".close-today")).Count > 0 ? "Closed" : "Open";
                    var city = address.Contains(",") ? address.Split(',')[1].Trim() : "Unknown";
                    var hours = store.FindElements(By.CssSelector(".open-today-hour")).Count > 0
                        ? store.FindElement(By.CssSelector(".open-today-hour")).Text.Trim()
                        : "Unknown";
                    var branchLink = store.FindElement(By.CssSelector("a")).GetAttribute("href");

                    storeLinks.Add((name, address, status, "No", city, hours, branchLink));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error extracting store info: {ex.Message}");
                }
            }

            return storeLinks;
        }

        private BranchData ProcessBranchDetails(IWebDriver driver, WebDriverWait wait, (string name, string address, string status, string is24Hours, string city, string hours, string link) storeInfo)
        {
            driver.Navigate().GoToUrl(storeInfo.link);
            wait.Until(d => d.FindElement(By.CssSelector("#branchPage")));

            var latitude = "0.0";
            var longitude = "0.0";

            try
            {
                var distanceElement = wait.Until(d => d.FindElement(By.CssSelector(".distance")));
                latitude = distanceElement.GetAttribute("data-latitude");
                longitude = distanceElement.GetAttribute("data-longitude");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not get coordinates for store {storeInfo.name}: {ex.Message}");
            }

            return new BranchData
            {
                Name = storeInfo.name,
                Address = storeInfo.address,
                Status = storeInfo.status,
                Is24Hours = storeInfo.is24Hours,
                City = storeInfo.city,
                Hours = storeInfo.hours,
                Longitude = longitude,
                Latitude = latitude
            };
        }

        private void LoadAllBranches(IWebDriver driver, WebDriverWait wait)
        {
            const int maxRetries = 3;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    Thread.Sleep(1000);
                    var loadMoreButton = wait.Until(d => d.FindElement(By.CssSelector(".btn-more")));

                    if (!loadMoreButton.Displayed)
                        break;

                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", loadMoreButton);
                    Thread.Sleep(500);
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", loadMoreButton);
                    Thread.Sleep(1500);
                }
                catch (WebDriverTimeoutException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading more branches (attempt {retryCount + 1}): {ex.Message}");
                    retryCount++;
                    if (retryCount >= maxRetries) break;
                    Thread.Sleep(2000);
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
