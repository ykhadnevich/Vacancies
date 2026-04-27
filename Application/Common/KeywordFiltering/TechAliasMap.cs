namespace Application.Common.KeywordFiltering;

public static class TechAliasMap
{
    public static readonly IReadOnlyDictionary<string, string[]> Aliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "golang",     ["golang", "go "] },
            { "javascript", ["javascript", " js "] },
            { "typescript", ["typescript", " ts "] },
            { "python",     ["python"] },
            { "dotnet",     [".net", "dotnet", "c#"] },
            { "java",       [" java "] },
            { "node",       ["node.js", "nodejs", "node "] },
        };

    public static string[] Resolve(string keyword) =>
        Aliases.TryGetValue(keyword, out var aliases) ? aliases : [keyword];
}
