using Application.Common.Observability;
using Sentry;

namespace API.Observability;

// Sentry BeforeSend hook — runs PiiScrubber over event message + exception chain.
internal static class SentryPiiScrubber
{
    public static SentryEvent? BeforeSend(SentryEvent sentryEvent, SentryHint _)
    {
        if (sentryEvent.Message is { } sentryMessage)
        {
            sentryMessage.Message   = PiiScrubber.Scrub(sentryMessage.Message);
            sentryMessage.Formatted = PiiScrubber.Scrub(sentryMessage.Formatted);
        }

        if (sentryEvent.SentryExceptions is { } exceptions)
        {
            foreach (var ex in exceptions)
            {
                if (ex.Value is { } value)
                    ex.Value = PiiScrubber.Scrub(value);
            }
        }

        return sentryEvent;
    }
}
