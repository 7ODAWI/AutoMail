namespace AutoMail.BulkEmail.Dto
{
    public class EmailSenderDto
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public bool EnableSsl { get; set; }
        public bool IsActive { get; set; }
        public int DailyLimit { get; set; }
        public int DelayBetweenEmailsMs { get; set; }
        public bool HasPassword { get; set; }
    }
}
