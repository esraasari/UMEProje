namespace UMEProje.Models
{
    public class LabClient
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        
        // Navigation property. Bu firmanın anketleri. 
        public ICollection<CalibrationSurvey> CalibrationSurveys { get; set; } = new List<CalibrationSurvey>();
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
