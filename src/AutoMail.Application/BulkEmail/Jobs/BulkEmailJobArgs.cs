using System;

namespace AutoMail.BulkEmail.Jobs
{
    [Serializable]
    public class BulkEmailJobArgs
    {
        public long OperationId { get; set; }
    }
}
