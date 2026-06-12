namespace Domain.Scoring;


/// <summary>
/// Canonical role family taxonomy used across the scoring pipeline.
///
/// Aliases (<see cref="Product"/>, <see cref="Generic"/>) exist for backward
/// compatibility with the previous Application-layer enum. They will be removed
/// in the post-Day 3 cleanup pass — callers should prefer the canonical names
/// (<see cref="ProductManagement"/>, <see cref="Other"/>).
/// </summary>
public enum RoleFamily
{

    Other              = 0,
    ProductManagement  = 1,
    Engineering        = 2,
    Design             = 3,
    Marketing          = 4,
    Data               = 5,
    DevOps             = 6,


    Generic = Other,
    Product = ProductManagement,
}
