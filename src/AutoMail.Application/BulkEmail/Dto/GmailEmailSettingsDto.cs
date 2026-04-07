namespace AutoMail.BulkEmail.Dto
{
    public class GmailEmailSettingsDto
    {
        public string UserName { get; set; }

        public string DefaultFromAddress { get; set; }

        public string DefaultFromDisplayName { get; set; }

        public string Host { get; set; }

        public int Port { get; set; }

        public bool EnableSsl { get; set; }

        public bool HasPassword { get; set; }
    }
}
