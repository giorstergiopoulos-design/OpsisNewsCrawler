namespace OpsisNewsCrawler.Core;

/// <summary>Turns the reasons of a <see cref="BreakingAssessment"/> into the short sentence the user reads ("3 sources in 4 min · official source"), in the language of the program.</summary>
public static class BreakingWhy
{
    public static string Text(IEnumerable<BreakingReason> reasons) =>
        string.Join(" · ", reasons.Select(Sentence).Where(s => s.Length > 0));

    private static string Sentence(BreakingReason r) => r.Code switch
    {
        "sources" => Loc.T("why.sources", r.Arg1 ?? "", r.Arg2 ?? ""),
        "fast" => Loc.T("why.fast", r.Arg1 ?? ""),
        "fresh" => Loc.T("why.fresh", r.Arg1 ?? ""),
        "agency" => Loc.T("why.agency"),
        "hazard" => string.IsNullOrEmpty(r.Arg2)
            ? Loc.T("why.hazard", Loc.T("hazard.kind." + r.Arg1))
            : Loc.T("why.hazardMagnitude", Loc.T("hazard.kind." + r.Arg1), r.Arg2),
        "keyword" => Loc.T("why.keyword"),
        "interest" => Loc.T("why.interest"),
        _ => ""
    };
}
