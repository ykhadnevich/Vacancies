namespace Application.Common.Interfaces;

/// <summary>
/// Forwards each LLM call to an external observability backend (LangSmith).
/// Designed as a strict no-op fallback when no key is configured — calling
/// <c>StartSpan</c> with a <see cref="NoopLlmTracer"/> returns a span whose
/// <c>EndOk</c> / <c>EndError</c> do nothing, so production code paths
/// remain identical regardless of whether tracing is enabled.
///
/// Typical usage in a Gemini-calling service:
/// <code>
///   using var span = _tracer.StartSpan(
///       name:    "monolithic_scoring",
///       runType: LlmRunType.LLM,
///       inputs:  new { cv = cvJson, vacancy = vacancyText, model = "gemini-2.5-flash" });
///   try
///   {
///       var result = await CallGeminiAsync(...);
///       span.EndOk(new { score = result.Score, tokensIn = inTok, tokensOut = outTok });
///       return result;
///   }
///   catch (Exception ex)
///   {
///       span.EndError(ex);
///       throw;
///   }
/// </code>
/// </summary>
public interface ILlmTracer
{
    /// <summary>Start a new traced span. Always returns a non-null span — even when the tracer is noop.</summary>
    ILlmSpan StartSpan(string name, LlmRunType runType, object inputs, string? parentRunId = null);

    /// <summary>Project / workspace identifier used when posting runs. Diagnostic-only.</summary>
    string ProjectName { get; }

    /// <summary>True when traces will actually be forwarded. False = noop mode.</summary>
    bool IsEnabled { get; }
}

/// <summary>
/// One traced LLM operation. Disposing without calling <c>EndOk</c> or
/// <c>EndError</c> is treated as an implicit success with empty outputs —
/// the implementation must NOT throw from Dispose, even on cancellation.
/// </summary>
public interface ILlmSpan : IDisposable
{
    /// <summary>Unique id of this run on the backend. Empty string when tracing is disabled.</summary>
    string RunId { get; }

    /// <summary>Mark the span as completed successfully and attach the LLM output payload.</summary>
    void EndOk(object outputs);

    /// <summary>Mark the span as failed. The exception's message + stack are recorded.</summary>
    void EndError(Exception error);

    /// <summary>Attach an arbitrary key/value tag (e.g. model version, token counts) to the span.</summary>
    void Tag(string key, object value);
}

/// <summary>
/// Mirrors LangSmith's run_type taxonomy so the dashboard can group calls correctly.
/// </summary>
public enum LlmRunType
{
    /// <summary>A single LLM API call — what Mono / Recruiter Mono / Judge / Reason / Extraction look like.</summary>
    LLM,
    /// <summary>A composed multi-step pipeline call — what a full v6 search or analyze-list looks like.</summary>
    Chain,
    /// <summary>A deterministic step (skill canonicalisation, scoring cap application) — diagnostic only.</summary>
    Tool
}
