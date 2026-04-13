namespace AutoMail.Project_Models
{
    public enum OperationStatus : byte
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        PartiallySent = 4
    }
}
