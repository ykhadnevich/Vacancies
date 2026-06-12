using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public sealed class VacancyNormalizationPostProcessor : IVacancyNormalizationPostProcessor
{

    private static readonly Dictionary<string, string> SkillCanon = new(StringComparer.OrdinalIgnoreCase)
    {
        [".net core"] = ".NET",
        [".net 8"] = ".NET",
        [".net 6"] = ".NET",
        [".net 7"] = ".NET",
        [".net 10"] = ".NET",
        [".net framework"] = ".NET Framework",
        [".net"] = ".NET",
        ["dotnet"] = ".NET",
        ["postgres"] = "PostgreSQL",
        ["postgresql"] = "PostgreSQL",
        ["nodejs"] = "Node.js",
        ["node.js"] = "Node.js",
        ["node js"] = "Node.js",
        ["k8s"] = "Kubernetes",
        ["kubernetes"] = "Kubernetes",
        ["minio"] = "MinIO",
        ["github actions"] = "GitHub Actions",
        ["githubactions"] = "GitHub Actions",
        ["gitlab ci"] = "GitLab CI",
        ["entity framework core"] = "EF Core",
        ["entity framework"] = "EF Core",
        ["ef core"] = "EF Core",
        ["asp.net core"] = "ASP.NET Core",
        ["asp net core"] = "ASP.NET Core",
        ["asp.net"] = "ASP.NET",
        ["c++"] = "C++",
        ["c#"] = "C#",
        ["багатопотоковість"] = "concurrency",
        ["чиста архітектура"] = "Clean Architecture",
        ["відмовостійкість"] = "fault tolerance",
        ["хмарні технології"] = "cloud",
        ["хмара"] = "cloud",
        ["1с"] = "1C",
        ["1s"] = "1C",
        ["тімлід"] = "Tech Lead",
        ["тех-лід"] = "Tech Lead"
    };


    private static readonly HashSet<string> SoftTraits = new(StringComparer.OrdinalIgnoreCase)
    {
        "комунікабельність", "communication", "communication skills",
        "відповідальність", "responsibility",
        "командний гравець", "team player", "teamwork",
        "уважність до деталей", "attention to detail",
        "проактивність", "proactivity", "high-agency",
        "бажання вчитися", "willingness to learn",
        "стресостійкість", "stress tolerance",
        "креативність", "creativity"
    };


    private static readonly HashSet<string> RedundantSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "engineer", "інженер"
    };


    private static readonly HashSet<string> AloneSpecialties = new(StringComparer.OrdinalIgnoreCase)
    {
        ".net", "devops", "ios", "android", "frontend", "backend", "fullstack",
        "qa", "qa automation"
    };


    private static readonly Dictionary<string, string> CityTransliteration = new()
    {
        ["Київ"] = "Kyiv",
        ["Львів"] = "Lviv",
        ["Харків"] = "Kharkiv",
        ["Одеса"] = "Odesa",
        ["Дніпро"] = "Dnipro",
        ["Запоріжжя"] = "Zaporizhzhia",
        ["Вінниця"] = "Vinnytsia",
        ["Чернівці"] = "Chernivtsi",
        ["Луцьк"] = "Lutsk",
        ["Тернопіль"] = "Ternopil",
        ["Івано-Франківськ"] = "Ivano-Frankivsk",
        ["Кривий Ріг"] = "Kryvyi Rih",
        ["Полтава"] = "Poltava",
        ["Суми"] = "Sumy",
        ["Київ"] = "Kyiv",
        ["Київ "] = "Kyiv",
        ["Киев"] = "Kyiv"
    };

    public string Process(string rawJson, string vacancyRawText)
    {
        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node is not JsonObject obj) return rawJson;


            obj["source_language"] = DetectLanguage(vacancyRawText);


            obj["must_have_skills"] = CanonAndFilter(obj["must_have_skills"], vacancyRawText);
            obj["nice_to_have_skills"] = CanonAndFilter(obj["nice_to_have_skills"], vacancyRawText);


            if (obj["role_title"] is JsonObject roleTitle)
            {
                roleTitle["en"] = StripRedundantSuffix(roleTitle["en"]?.GetValue<string>() ?? "");
                roleTitle["uk"] = StripRedundantSuffix(roleTitle["uk"]?.GetValue<string>() ?? "");
            }


            if (obj["location"] is JsonObject loc)
            {
                var cityUk = loc["city_uk"]?.GetValue<string>();
                var cityEn = loc["city_en"]?.GetValue<string>();
                if (string.IsNullOrEmpty(cityEn) && !string.IsNullOrEmpty(cityUk)
                    && CityTransliteration.TryGetValue(cityUk, out var translit))
                {
                    loc["city_en"] = translit;
                }

                if (cityUk == "Киев") loc["city_uk"] = "Київ";
            }

            return obj.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch
        {

            return rawJson;
        }
    }

    private static JsonArray CanonAndFilter(JsonNode? arrayNode, string vacancyRawText)
    {
        var result = new JsonArray();
        if (arrayNode is not JsonArray arr) return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in arr)
        {
            var raw = item?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var trimmed = raw.Trim();


            if (SoftTraits.Contains(trimmed)) continue;


            var canon = SkillCanon.TryGetValue(trimmed, out var c) ? c : trimmed;


            if (IsBrandLikeToken(canon)
                && !AppearsInText(canon, vacancyRawText)
                && !AppearsInText(trimmed, vacancyRawText))
            {
                continue;
            }


            if (seen.Add(canon))
                result.Add(canon);
        }
        return result;
    }


    private static bool IsBrandLikeToken(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill)) return false;
        if (skill.Contains(' ')) return false;


        if (skill.Length <= 5 && skill.All(c => char.IsUpper(c) || char.IsDigit(c)))
            return true;


        return System.Text.RegularExpressions.Regex.IsMatch(
            skill,
            @"^[A-Z.][a-zA-Z0-9.+#\-]*$");
    }


    private static bool AppearsInText(string skill, string text)
    {
        if (string.IsNullOrEmpty(skill) || string.IsNullOrEmpty(text)) return false;
        return text.Contains(skill, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripRedundantSuffix(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return title;

        var tokens = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2) return title;

        var last = tokens[^1];
        if (!RedundantSuffixes.Contains(last)) return title;


        var stemTokens = tokens
            .Take(tokens.Length - 1)
            .Where(t => !IsSeniorityToken(t))
            .ToArray();
        if (stemTokens.Length == 0) return title;

        var stem = string.Join(" ", stemTokens).ToLowerInvariant();
        if (AloneSpecialties.Contains(stem))
            return string.Join(" ", tokens[..^1]);

        return title;
    }

    private static bool IsSeniorityToken(string t) => t.ToLowerInvariant() switch
    {
        "junior" or "middle" or "mid" or "senior" or "lead" or "principal"
            or "staff" or "intern" or "trainee" or "strong" or "head" or "chief"
            or "молодший" or "старший" or "провідний" or "стажер" => true,
        _ => false
    };

    private static string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown";
        int cyrillic = 0, latin = 0;
        foreach (var c in text)
        {
            if ((c >= 'А' && c <= 'я')
                || c == 'і' || c == 'І'
                || c == 'ї' || c == 'Ї'
                || c == 'є' || c == 'Є'
                || c == 'ґ' || c == 'Ґ')
                cyrillic++;
            else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                latin++;
        }
        if (cyrillic + latin == 0) return "unknown";
        if (cyrillic > latin * 2) return "uk";
        if (latin > cyrillic * 2) return "en";
        return "mixed";
    }
}
