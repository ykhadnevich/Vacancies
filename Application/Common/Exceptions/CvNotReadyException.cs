namespace Application.Common.Exceptions;


public sealed class CvNotReadyException : Exception
{
    public CvNotReadyException()
        : base("CV has been uploaded but not yet normalized into structured JSON. " +
               "Call POST /api/user/cv/normalize or wait for the background worker, " +
               "then retry. To use raw CV scoring intentionally, pass cvVersion=Raw.")
    {
    }
}
