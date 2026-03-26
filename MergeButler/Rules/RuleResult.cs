namespace MergeButler.Rules;

public sealed record RuleResult(bool Approved, string RuleName, string Reason);
