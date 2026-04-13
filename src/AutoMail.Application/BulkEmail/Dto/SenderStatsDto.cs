namespace AutoMail.BulkEmail.Dto
{
    public class SenderStatsDto
    {
        public int SenderId { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public int SentToday { get; set; }
        public int RemainingQuota { get; set; }
        public int DailyLimit { get; set; }
        public bool IsActive { get; set; }
    }
}
