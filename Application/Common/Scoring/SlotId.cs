namespace Application.Common.Scoring;


public readonly record struct SlotId(string Id)
{
    public override string ToString() => Id;


    public static readonly SlotId Header              = new("S001_HEADER");
    public static readonly SlotId OutputSpec          = new("S002_OUTPUT_SPEC");
    public static readonly SlotId PreComputedYears    = new("S003_PRE_COMPUTED_YEARS");


    public static readonly SlotId HardCapsStep1       = new("S004_HARD_CAPS_STEP1");
    public static readonly SlotId HardCapsStep2Map    = new("S005_HARD_CAPS_STEP2_MAPPING");
    public static readonly SlotId HardCapsStep3       = new("S006_HARD_CAPS_STEP3");

    public static readonly SlotId MidSeniorJuniorCap  = new("S007A_MID_SENIOR_JUNIOR_CAP");
    public static readonly SlotId OverqualifiedCap    = new("S007B_OVERQUALIFIED_CAP");
    public static readonly SlotId EngineeringMgrRule  = new("S007C_ENGINEERING_MANAGER_RULE");
    public static readonly SlotId CoreFunctionMismatch = new("S007D_CORE_FUNCTION_MISMATCH");


    public static readonly SlotId MismatchExamples    = new("S008_MISMATCH_EXAMPLES");
    public static readonly SlotId JuniorFriendly      = new("S009_JUNIOR_FRIENDLY");
    public static readonly SlotId FamilyBoost         = new("S010_FAMILY_BOOST");


    public static readonly SlotId VerdictBands        = new("S011_VERDICT_BANDS");
    public static readonly SlotId ScorePrecision      = new("S012_SCORE_PRECISION");
    public static readonly SlotId ExperienceMultipliers = new("S013_EXPERIENCE_MULTIPLIERS");


    public static readonly SlotId LanguageHandling    = new("S014_LANGUAGE_HANDLING");
    public static readonly SlotId CareerSwitcherGen   = new("S015_CAREER_SWITCHER_GENERAL");
    public static readonly SlotId CareerSwitcherFam   = new("S016_CAREER_SWITCHER_FAMILY");
    public static readonly SlotId EagerToLearn        = new("S017_EAGER_TO_LEARN");


    public static readonly SlotId PlatformToolsRule   = new("S018_PLATFORM_TOOLS_RULE");
    public static readonly SlotId PlatformToolsList   = new("S019_PLATFORM_TOOLS_LIST");
    public static readonly SlotId DomainLock          = new("S020_DOMAIN_LOCK");


    public static readonly SlotId ToolWeightMeta      = new("S021_TOOL_WEIGHT_META");
    public static readonly SlotId ToolWeightList      = new("S022_TOOL_WEIGHT_LIST");
    public static readonly SlotId MatchedGapsRules    = new("S023_MATCHED_GAPS_RULES");


    public static readonly SlotId Finale              = new("S024_FINALE");


    public static readonly IReadOnlyList<SlotId> AllInOrder = new[]
    {
        Header, OutputSpec, PreComputedYears,
        HardCapsStep1, HardCapsStep2Map, HardCapsStep3,
        MidSeniorJuniorCap, OverqualifiedCap, EngineeringMgrRule, CoreFunctionMismatch,
        MismatchExamples, JuniorFriendly, FamilyBoost,
        VerdictBands, ScorePrecision, ExperienceMultipliers,
        LanguageHandling, CareerSwitcherGen, CareerSwitcherFam, EagerToLearn,
        PlatformToolsRule, PlatformToolsList, DomainLock,
        ToolWeightMeta, ToolWeightList, MatchedGapsRules,
        Finale
    };


    public static readonly IReadOnlySet<SlotId> KnownSet = new HashSet<SlotId>(AllInOrder);
}
