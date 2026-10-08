// Pure managed modifier evaluation. Game objects never enter this model.
internal enum ModifierUnit { Percent, Flat, Seconds, Multiplier }
internal enum ModifierLayer { Additive, Base, Independent, Delivery, Defense, Resource }
internal enum ModifierCondition { Always, Scoped, Magazine, BuffStacks, BuffPresence, Resource, BuffResource, Unresolved }
internal enum ModifierCoverage { Calculated, Partial, NeedsState, NeedsFormula, NoNumericModifier, MissingDefinition }
internal sealed record ModifierRule(string Metric, double Amount, ModifierUnit Unit, ModifierLayer Layer,
    string Scope, ModifierCondition Condition, string Requirement = "", double? Cap = null, int[]? StateIds = null,
    string Resource = "", double ResourceStep = 1, double PerResource = 0, int? StackLimit = null, bool WholeSteps = false);
internal sealed record ModifierDefinition(string Name, string Description, ModifierRule[] Rules, string Review);
internal sealed record EquippedModifier(string Source, int Id, int Level, string Category);
internal sealed record CoverageFinding(string Source, int Id, int Level, string Name, ModifierCoverage Status, string Reason, string Category = "");
internal static class ModifierEvaluator
{
    internal static double? Value(ModifierRule rule, int? magazine, int? stacks = null, double? resource = null)
    {
        if (resource.HasValue && (!double.IsFinite(resource.Value) || resource.Value<0)) resource=null;
        if (rule.ResourceStep<=0) return null;
        if (rule.WholeSteps && resource.HasValue && Math.Abs(resource.Value/rule.ResourceStep-Math.Round(resource.Value/rule.ResourceStep))>1e-6) resource=null;
        if (stacks.HasValue && rule.StackLimit.HasValue) stacks=Math.Clamp(stacks.Value,0,rule.StackLimit.Value);
        double? value = rule.Condition switch {
            ModifierCondition.Always or ModifierCondition.Scoped => rule.Amount,
            ModifierCondition.Magazine => magazine.HasValue ? Math.Max(0, magazine.Value) * rule.Amount : null,
            ModifierCondition.BuffStacks => stacks.HasValue ? Math.Max(0,stacks.Value) * rule.Amount : null,
            ModifierCondition.BuffPresence => stacks.HasValue ? (stacks.Value>0 ? rule.Amount : 0) : null,
            ModifierCondition.Resource => resource.HasValue ? rule.Amount + resource.Value/rule.ResourceStep*rule.PerResource : null,
            ModifierCondition.BuffResource => stacks==0 ? 0 : stacks.HasValue && resource.HasValue ? Math.Max(0,stacks.Value)*(rule.Amount+resource.Value/rule.ResourceStep*rule.PerResource) : null,
            _ => null
        };
        if (value.HasValue && rule.Cap.HasValue) value = Math.Min(value.Value, rule.Cap.Value);
        return value;
    }
    internal static CoverageFinding Coverage(EquippedModifier item, ModifierDefinition? definition, int? magazine)
    {
        if (definition == null) return new(item.Source,item.Id,item.Level,$"{item.Source} {item.Id}",ModifierCoverage.MissingDefinition,"Definition absent from this game-build catalogue.");
        if (definition.Rules.Length == 0)
        {
            var status = definition.Review == "no-numeric" ? ModifierCoverage.NoNumericModifier :
                definition.Review == "needs-state" ? ModifierCoverage.NeedsState : ModifierCoverage.NeedsFormula;
            return new(item.Source,item.Id,item.Level,definition.Name,status,definition.Description);
        }
        if (definition.Rules.Any(rule => !Value(rule,magazine).HasValue))
            return new(item.Source,item.Id,item.Level,definition.Name,ModifierCoverage.NeedsState,"Required runtime measurement unavailable; excluded from numeric totals.");
        return new(item.Source,item.Id,item.Level,definition.Name,definition.Review == "partial" ? ModifierCoverage.Partial : ModifierCoverage.Calculated,
            definition.Review == "partial" ? "Some clauses calculated; remaining effects need formula/state review. " + definition.Description : "All represented clauses calculated; target/trigger scope retained.");
    }
}
