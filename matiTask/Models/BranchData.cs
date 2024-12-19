namespace SuperPharmScraper.Models
{
    public class BranchData
    {
        public required string Name { get; set; } = "לא זמין";
        public required string Longitude { get; set; } = "0.0";
        public required string Latitude { get; set; }= "0.0";
        public required string Address { get; set; }= "לא זמין";
        public required string Hours { get; set; }= "לא זמין";
        public required string Is24Hours { get; set; }= "לא";
        public required string City { get; set; } = "לא זמין";
        public  string? Status { get; internal set; } = "לא זמין";
    }
}
