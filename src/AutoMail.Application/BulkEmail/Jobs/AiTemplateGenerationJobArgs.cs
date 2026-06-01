using System;

namespace AutoMail.BulkEmail.Jobs
{
    [Serializable]
    public class AiTemplateGenerationJobArgs
    {
        public long OperationId { get; set; }
    }
}