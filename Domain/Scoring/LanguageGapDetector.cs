using System.Text.Json;

namespace Domain.Scoring;


public static class LanguageGapDetector
{


    public static bool IsLanguageRequirementAbove(JsonElement cv, JsonElement vacancy)
    {
        string? vacReq = null, cvLvl = null;
        if (vacancy.ValueKind == JsonValueKind.Object
            && vacancy.TryGetProperty("english_required", out var vr)
            && vr.ValueKind == JsonValueKind.String)
            vacReq = vr.GetString();
        if (cv.ValueKind == JsonValueKind.Object
            && cv.TryGetProperty("english_level", out var cl)
            && cl.ValueKind == JsonValueKind.String)
            cvLvl = cl.GetString();

        int vReq = RankEnglish(vacReq);
        int vCv  = RankEnglish(cvLvl);
        return vReq > 0 && vReq > vCv;
    }


    public static int RankEnglish(string? level) => (level?.Trim().ToUpperInvariant()) switch
    {
        "A1" => 1, "A2" => 2,
        "B1" => 3, "B2" => 4,
        "C1" => 5, "C2" => 6,
        _    => 0
    };
}
