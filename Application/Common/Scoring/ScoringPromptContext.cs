using System.Collections.Concurrent;

namespace Application.Common.Scoring;


public sealed class ScoringPromptContext
{
    public string CvText { get; }
    public string JobTitle { get; }
    public string JobCompany { get; }
    public string JobDescription { get; }
    public RoleWeightedYears? RoleYears { get; }

    public ScoringPromptContext(
        string cvText,
        string jobTitle,
        string jobCompany,
        string jobDescription,
        RoleWeightedYears? roleYears)
    {
        CvText         = cvText;
        JobTitle       = jobTitle ?? string.Empty;
        JobCompany     = jobCompany ?? string.Empty;
        JobDescription = jobDescription ?? string.Empty;
        RoleYears      = roleYears;
    }


    private readonly ConcurrentDictionary<string, object> _moduleState = new();


    public T GetOrComputeModuleState<T>(string key, Func<T> compute) where T : notnull =>
        (T)_moduleState.GetOrAdd(key, _ => compute());
}
