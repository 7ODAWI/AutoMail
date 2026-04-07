using System;

namespace AutoMail.BulkEmail.Jobs
{
    /// <summary>
    /// Arguments passed to <see cref="BulkEmailSenderJob"/>.
    /// Serialised by ABP and stored in the background jobs table.
    /// </summary>
    [Serializable]
    public class BulkEmailJobArgs
    {
        public string Subject { get; set; }
        public string Body { get; set; }
    }
}
