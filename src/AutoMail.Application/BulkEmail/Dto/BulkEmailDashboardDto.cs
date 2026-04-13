using System.Collections.Generic;

namespace AutoMail.BulkEmail.Dto
{
    public class BulkEmailDashboardDto
    {
        public int TotalPending { get; set; }
        public int TotalSentToday { get; set; }
        public int TotalSentAllTime { get; set; }
        public int TotalFailedToday { get; set; }
        public int TotalPermanentlyFailed { get; set; }
        public List<SenderStatsDto> SenderBreakdown { get; set; } = new();
    }
}
