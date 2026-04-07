namespace AutoMail.BulkEmail.Dto
{
    public class UploadEmailsResult
    {
        /// <summary>Total rows parsed from the file.</summary>
        public int ParsedCount { get; set; }

        /// <summary>Rows skipped due to invalid or duplicate email.</summary>
        public int SkippedCount { get; set; }

        /// <summary>New emails successfully persisted to the database.</summary>
        public int SavedCount { get; set; }
    }
}
