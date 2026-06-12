using Application.Common.Interfaces;
using Application.Common.Scoring;
using System.Text;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public sealed class SlotComposer
{

    public string Compose(ScoringPromptContext ctx, IScoringModule module)
    {
        var moduleSlots = module.GetSlots(ctx) ??
                          new Dictionary<SlotId, SlotContent>();


        foreach (var id in moduleSlots.Keys)
        {
            if (!SlotId.KnownSet.Contains(id))
            {
                throw new InvalidOperationException(
                    $"Module {module.GetType().Name} (family {module.Family}) returned " +
                    $"unknown SlotId '{id}'. Known IDs: " +
                    string.Join(", ", SlotId.AllInOrder.Select(s => s.Id)));
            }
        }

        var sb = new StringBuilder();
        var firstAppended = true;

        foreach (var slotId in SlotId.AllInOrder)
        {
            var coreDefault = PromptCore.BuildDefault(slotId, ctx);
            var structured  = RenderStructured(slotId, ctx, module);
            moduleSlots.TryGetValue(slotId, out var moduleContent);

            var slotText = ComposeOne(coreDefault, structured, moduleContent);
            if (string.IsNullOrEmpty(slotText)) continue;

            if (!firstAppended) sb.Append("\n\n");
            sb.Append(slotText);
            firstAppended = false;
        }

        return sb.ToString();
    }


    public static string ComposeOne(string coreDefault, string structured, SlotContent? moduleContent)
    {
        var policy = moduleContent?.Policy ?? SlotPolicy.Append;

        if (policy == SlotPolicy.Skip) return string.Empty;

        if (policy == SlotPolicy.Replace)
            return moduleContent?.Text ?? string.Empty;


        var baseText = string.IsNullOrEmpty(structured)
            ? coreDefault
            : (string.IsNullOrEmpty(coreDefault) ? structured : coreDefault + "\n" + structured);

        var moduleText = moduleContent?.Text ?? string.Empty;
        if (string.IsNullOrEmpty(moduleText)) return baseText;

        return policy switch
        {
            SlotPolicy.Prepend => moduleText + "\n" + baseText,
            _                  => baseText  + "\n" + moduleText
        };
    }


    private static string RenderStructured(SlotId slotId, ScoringPromptContext ctx, IScoringModule module)
    {
        if (slotId == SlotId.HardCapsStep2Map)
            return RenderBucketMappingsAndAdjacency(module);

        if (slotId == SlotId.MismatchExamples)
            return RenderMismatchExamples(module.GetMismatchList());

        if (slotId == SlotId.CareerSwitcherFam)
            return RenderCareerPatterns(module.GetCareerPatterns());

        if (slotId == SlotId.ToolWeightList)
            return RenderToolWeights(module.GetToolWeights(ctx));

        return string.Empty;
    }

    private static string RenderBucketMappingsAndAdjacency(IScoringModule module)
    {
        var mappings = module.GetBucketMappings() ?? Array.Empty<RoleBucketMapping>();
        var adjacency = module.GetAdjacencyRules() ?? Array.Empty<AdjacencyRule>();

        if (mappings.Count == 0 && adjacency.Count == 0) return string.Empty;

        var sb = new StringBuilder();

        if (mappings.Count > 0)
        {
            sb.AppendLine("  Role mappings (Job requires X → use Y weighted years):");
            foreach (var m in mappings)
            {
                sb.Append("    ");
                sb.Append(m.JobRolePattern);
                sb.Append(" → use '");
                sb.Append(m.Bucket.Id);
                sb.Append("' bucket");
                if (!string.IsNullOrEmpty(m.Note))
                {
                    sb.Append("   (");
                    sb.Append(m.Note);
                    sb.Append(')');
                }
                sb.AppendLine();
            }
        }

        if (adjacency.Count > 0)
        {
            if (mappings.Count > 0) sb.AppendLine();
            sb.AppendLine("  FRAMEWORK ADJACENCY (partial-match penalties):");
            foreach (var a in adjacency)
            {
                sb.Append("    ");
                if (a.Direction == AdjacencyDirection.Symmetric)
                    sb.Append(a.FromTech).Append(" ↔ ").Append(a.ToTech);
                else
                    sb.Append("Candidate has ").Append(a.FromTech)
                      .Append(", job requires ").Append(a.ToTech);

                sb.Append(": penalty ").Append(a.PenaltyMin).Append('-').Append(a.PenaltyMax);
                if (!string.IsNullOrEmpty(a.Note))
                    sb.Append("   (").Append(a.Note).Append(')');
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string RenderMismatchExamples(IReadOnlyList<MismatchExample> examples)
    {
        if (examples is null || examples.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("  Family-specific mismatch examples (titles that LOOK adjacent but are NOT):");
        foreach (var ex in examples)
        {
            sb.Append("    ").Append(ex.Title).Append(" → ").AppendLine(ex.ActualWork);
        }
        return sb.ToString().TrimEnd();
    }

    private static string RenderCareerPatterns(IReadOnlyList<CareerPattern> patterns)
    {
        if (patterns is null || patterns.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("  Career-transition patterns for this family:");
        foreach (var p in patterns)
        {
            sb.Append("    ").Append(p.FromRole).Append(" → ").Append(p.ToRole).Append(": ");

            if (p.RequiredSignals.Count > 0)
            {
                sb.Append("if CV shows [").Append(string.Join(", ", p.RequiredSignals)).Append("] → ");
                sb.Append(FormatAdjustment(p.ScoreIfSignalsPresent));
                sb.Append(". Otherwise → ");
                sb.Append(FormatAdjustment(p.ScoreIfSignalsAbsent));
            }
            else
            {
                sb.Append(FormatAdjustment(p.ScoreIfSignalsPresent));
            }

            if (!string.IsNullOrEmpty(p.Note))
                sb.Append(" (").Append(p.Note).Append(')');
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string FormatAdjustment(int delta)
    {
        if (delta == 0) return "no penalty";
        return $"{delta:+0;-0} score adjustment";
    }

    private static string RenderToolWeights(IReadOnlyDictionary<string, ToolWeight> weights)
    {
        if (weights is null || weights.Count == 0) return string.Empty;

        var hard = weights.Where(kv => kv.Value == ToolWeight.Hard)
                          .Select(kv => kv.Key)
                          .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                          .ToList();
        var easy = weights.Where(kv => kv.Value == ToolWeight.Easy)
                          .Select(kv => kv.Key)
                          .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                          .ToList();

        if (hard.Count == 0 && easy.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("  Family-specific tool weights:");
        if (hard.Count > 0)
            sb.Append("    Hard (absence = critical/moderate when required): ")
              .AppendLine(string.Join(", ", hard));
        if (easy.Count > 0)
            sb.Append("    Easy (absence = minor only): ")
              .AppendLine(string.Join(", ", easy));
        return sb.ToString().TrimEnd();
    }
}
