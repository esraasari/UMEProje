namespace UMEProje.Models
{
    public class CalibrationSurvey
    {
        public int Id { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string LabCategory { get; set; } = string.Empty;
        public bool IsApproved { get; set; } = false;
        public int LabClientId { get; set; }
        
        // Navigation property. Anketin hangi firmaya ait olduğunu gösterir.
        public LabClient? LabClient { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    }
}
