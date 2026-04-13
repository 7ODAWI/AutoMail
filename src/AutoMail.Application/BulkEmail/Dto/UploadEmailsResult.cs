namespace AutoMail.BulkEmail.Dto
{
    public class UploadEmailsResult
    {
        /// <summary>Total non-empty rows parsed from the file.</summary>
        public int ParsedCount { get; set; }

        /// <summary>Rows that contained an invalid email address format.</summary>
        public int InvalidCount { get; set; }

        /// <summary>Valid emails skipped because they were duplicates within the file or already exist in the database.</summary>
        public int SkippedCount { get; set; }

        /// <summary>New emails successfully persisted to the database.</summary>
        public int SavedCount { get; set; }
    }
}
