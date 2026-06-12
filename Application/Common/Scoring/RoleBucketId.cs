namespace Application.Common.Scoring;


public readonly record struct RoleBucketId(string Id)
{

    public static readonly RoleBucketId PmPo            = new("pm_po");
    public static readonly RoleBucketId Pmm             = new("pmm");
    public static readonly RoleBucketId BusinessAnalyst = new("business_analyst");
    public static readonly RoleBucketId ProjectManager  = new("project_manager");
    public static readonly RoleBucketId Developer       = new("developer");
    public static readonly RoleBucketId DataAnalyst     = new("data_analyst");
    public static readonly RoleBucketId Designer        = new("designer");
    public static readonly RoleBucketId Marketing       = new("marketing");


    public static readonly RoleBucketId Backend         = new("backend");
    public static readonly RoleBucketId Frontend        = new("frontend");
    public static readonly RoleBucketId Fullstack       = new("fullstack");
    public static readonly RoleBucketId Mobile          = new("mobile");
    public static readonly RoleBucketId DevOps          = new("devops");
    public static readonly RoleBucketId Qa              = new("qa");
    public static readonly RoleBucketId MlEngineer      = new("ml_engineer");
    public static readonly RoleBucketId DataEngineer    = new("data_engineer");
    public static readonly RoleBucketId Embedded        = new("embedded");
    public static readonly RoleBucketId SecurityEng     = new("security_eng");


    public static readonly IReadOnlyList<RoleBucketId> PmFamilyV1 = new[]
    {
        PmPo, Pmm, BusinessAnalyst, ProjectManager, Developer, DataAnalyst, Designer, Marketing
    };

    public override string ToString() => Id;
}
