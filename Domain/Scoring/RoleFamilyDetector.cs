using System.Text.Json;

namespace Domain.Scoring;


public static class RoleFamilyDetector
{


    public static RoleFamily Detect(JsonElement cv)
    {
        if (cv.ValueKind != JsonValueKind.Object) return RoleFamily.Other;
        if (!cv.TryGetProperty("target_roles", out var tr)
            || tr.ValueKind != JsonValueKind.Array) return RoleFamily.Other;

        foreach (var role in tr.EnumerateArray())
        {
            if (role.ValueKind != JsonValueKind.String) continue;
            var s = role.GetString()?.ToLowerInvariant() ?? "";

            if (s.Contains("product manager") || s.Contains("product owner")
                || s.Contains("head of product") || s.Contains("cpo")
                || s.Contains("продакт") || s.Contains("продукт-менеджер")
                || s.Contains("менеджер продукт"))
                return RoleFamily.ProductManagement;

            if (s.Contains("designer") || s.Contains(" ux") || s.Contains(" ui ")
                || s.StartsWith("ui ") || s.StartsWith("ux ") || s.EndsWith(" ux")
                || s.Contains("дизайнер"))
                return RoleFamily.Design;

            if (s.Contains("data engineer") || s.Contains("data analyst")
                || s.Contains("data scientist") || s.Contains("machine learning")
                || s.Contains("ml engineer") || s.Contains("ai engineer")
                || s.Contains("analytics") || s.Contains(" bi ") || s.EndsWith(" bi")
                || s.Contains("аналітик") || s.Contains("дата"))
                return RoleFamily.Data;

            if (s.Contains("devops") || s.Contains("sre") || s.Contains("site reliability")
                || s.Contains("cloud engineer") || s.Contains("cloud architect")
                || s.Contains("platform engineer") || s.Contains("infrastructure")
                || s.Contains("kubernetes"))
                return RoleFamily.DevOps;

            if (s.Contains("marketing") || s.Contains("marketer") || s.Contains("growth")
                || s.Contains("pmm") || s.Contains("smm") || s.Contains("brand")
                || s.Contains("маркет") || s.Contains("бренд"))
                return RoleFamily.Marketing;


            if (s.Contains("cybersecurity") || s.Contains("cyber security")
                || s.Contains("infosec") || s.Contains("information security")
                || s.Contains("security engineer") || s.Contains("penetration")
                || s.Contains("кібербезпек")
                || s.Contains("qa ") || s.StartsWith("qa") || s.EndsWith(" qa")
                || s.Contains("quality assurance") || s.Contains("automation engineer")
                || s.Contains("test engineer") || s.Contains("sdet")
                || s.Contains("тестув")
                || s.Contains("solutions architect") || s.Contains("enterprise architect")
                || s.Contains("technical architect")
                || s.Contains("project manager") || s.Contains("project lead")
                || s.Contains("delivery manager") || s.Contains("scrum master")
                || s.Contains("проект")
                || s.Contains("sales") || s.Contains("account executive")
                || s.Contains("account manager") || s.Contains("bdr")
                || s.Contains("sdr") || s.Contains("business development")
                || s.Contains("продаж"))
                return RoleFamily.Other;

            if (s.Contains("engineer") || s.Contains("developer") || s.Contains("programmer")
                || s.Contains("backend") || s.Contains("frontend") || s.Contains("fullstack")
                || s.Contains("full-stack") || s.Contains("full stack")
                || s.Contains("mobile") || s.Contains(" ios") || s.Contains("android")
                || s.Contains("tech lead") || s.Contains("architect")
                || s.Contains("розробник") || s.Contains("програміст") || s.Contains("інженер"))
                return RoleFamily.Engineering;
        }

        return RoleFamily.Other;
    }
}
