namespace Domain.Constants;

public static class PipelineSteps
{
    public const string CvSent = "CvSent";
    public const string Responded = "Responded";
    public const string FollowUpSent = "FollowUpSent";
    public const string ShortInterview = "ShortInterview";
    public const string TestTask = "TestTask";
    public const string TechnicalInterview = "TechnicalInterview";
    public const string FinalInterview = "FinalInterview";
    public const string JobOffer = "JobOffer";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CvSent,
        Responded,
        FollowUpSent,
        ShortInterview,
        TestTask,
        TechnicalInterview,
        FinalInterview,
        JobOffer
    };
}
