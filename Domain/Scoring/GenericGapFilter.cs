using System;
using System.Collections.Generic;
using System.Linq;

namespace Domain.Scoring;


public static class GenericGapFilter
{
    private static readonly HashSet<string> Blacklist =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "rest api", "rest apis", "restful api", "restful apis",
            "web api", "web apis", "api", "apis",
            "repos", "repo", "repositories", "repository",
            "troubleshooting", "debugging", "problem solving",
            "ci/cd", "continuous integration", "continuous delivery",
            "containerized applications", "containers",
            "cloud messaging", "service architecture",
            "monitoring", "logging",
            "agile", "scrum", "teamwork", "communication",
        };

    public static List<string> Filter(IEnumerable<string> gaps) =>
        gaps
            .Where(g => !string.IsNullOrWhiteSpace(g)
                     && !Blacklist.Contains(g.Trim()))
            .Select(g => g.Trim())
            .ToList();
}
