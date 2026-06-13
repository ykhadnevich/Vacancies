using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace Infrastructure.Resilience;

public static class GeminiRetryPolicy
{
    private const int DefaultRetryCount = 3;
    private const double BackoffBaseSeconds = 2.0;
    private const int JitterMaxMs = 1000;

    public static IAsyncPolicy<HttpResponseMessage> Build(
        ILoggerFactory loggerFactory,
        int? retryCount = null)
    {
        var logger = loggerFactory.CreateLogger("GeminiRetryPolicy");
        var attempts = retryCount ?? DefaultRetryCount;

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount: attempts,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(BackoffBaseSeconds, attempt)) +
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, JitterMaxMs)),
                onRetry: (outcome, delay, attempt, ctx) =>
                {
                    var status = outcome.Result?.StatusCode.ToString() ?? "no-response";
                    var ex     = outcome.Exception?.GetType().Name ?? "—";
                    logger.LogWarning(
                        "Gemini retry {Attempt}/{Max} in {DelayMs}ms — status={Status}, exception={Exception}",
                        attempt, attempts, (int)delay.TotalMilliseconds, status, ex);
                });
    }
}

public static class HttpClientBuilderGeminiExtensions
{
    public static IHttpClientBuilder AddGeminiRetry(this IHttpClientBuilder builder)
        => builder.AddPolicyHandler((sp, _) =>
            sp.GetRequiredService<IAsyncPolicy<HttpResponseMessage>>());
}
