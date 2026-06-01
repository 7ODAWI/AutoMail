namespace AutoMail.Project_Models
{
    public enum AiGenerationRunStatus : byte
    {
        Pending = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4
    }
}