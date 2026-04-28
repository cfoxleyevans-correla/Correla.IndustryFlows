using System.Text.Json;
using System.Text.RegularExpressions;
using Correla.IndustryFlows.Dtc.Schema;

namespace Correla.IndustryFlows.Dtc.Validation;

/// <summary>
/// Evaluates the declarative rule pack embedded in a <see cref="FlowSchema"/> against
/// a parsed <see cref="DtcFile"/>. Rules fire once per instance of their scoped group.
/// </summary>
public sealed class RuleEngine
{
    private readonly IPredicateRegistry _predicates;

    /// <summary>Initialises the engine with the predicate registry to use for <c>satisfies</c> operators.</summary>
    public RuleEngine(IPredicateRegistry predicates)
    {
        _predicates = predicates;
    }

    /// <summary>
    /// Evaluates all rules in <paramref name="schema"/> against the parsed <paramref name="file"/>.
    /// </summary>
    public IReadOnlyList<Finding> Evaluate(
        DtcFile file,
        FlowSchema schema,
        ProcessingContext? context = null)
    {
        var findings = new List<Finding>();

        foreach (var rule in schema.Rules)
        {
            // Collect all group instances with the rule's scope code.
            var instances = CollectInstances(file.Root, rule.Scope);

            foreach (var instance in instances)
            {
                EvaluateRule(rule, instance, context, findings);
            }
        }

        return findings.AsReadOnly();
    }

    // ---- Rule evaluation ----

    /// <summary>Evaluates a single rule against a single group instance.</summary>
    private void EvaluateRule(
        Rule rule,
        GroupInstance instance,
        ProcessingContext? context,
        List<Finding> findings)
    {
        // Evaluate the optional 'when' precondition.
        if (rule.When.HasValue && !EvaluateCondition(rule.When.Value, instance, context))
        {
            return; // Precondition not met — skip this rule for this instance.
        }

        // Evaluate the 'expect' assertion.
        if (!EvaluateCondition(rule.Expect, instance, context))
        {
            var severity = rule.Severity.ToLowerInvariant() switch
            {
                "warning" => Severity.Warning,
                "info" => Severity.Info,
                _ => Severity.Error,
            };

            findings.Add(new Finding(
                rule.Id, severity,
                $"{rule.Scope}[line {instance.LineNumber}]",
                rule.Message,
                instance.LineNumber));
        }
    }

    /// <summary>
    /// Evaluates a condition JSON element (a <c>when</c> or <c>expect</c> object) against the instance.
    /// Supports operators: equals, notEquals, in, notIn, present, matches, satisfies.
    /// Supports special 'context' key for ProcessingContext lookups.
    /// Supports 'child' key to look into a child group.
    /// </summary>
    private bool EvaluateCondition(
        JsonElement condition,
        GroupInstance instance,
        ProcessingContext? context)
    {
        // Resolve the target instance — may be redirected to a child group.
        var target = instance;
        if (condition.TryGetProperty("child", out var childProp))
        {
            var childCode = childProp.GetString()!;
            target = instance.Children.FirstOrDefault(c => c.GroupCode == childCode);
            if (target is null)
            {
                // Child doesn't exist; evaluate against a virtual absent instance.
                // Only the 'present' operator makes sense here.
                if (condition.TryGetProperty("present", out var presentProp))
                {
                    return !presentProp.GetBoolean(); // Expect absent = true when present:false.
                }

                return false;
            }
        }

        // Resolve the field value (or context value).
        string? fieldValue = null;
        bool isContextLookup = false;

        if (condition.TryGetProperty("context", out var ctxProp))
        {
            isContextLookup = true;
            var ctxKey = ctxProp.GetString()!;
            fieldValue = ResolveContextValue(ctxKey, context, instance);
        }
        else if (condition.TryGetProperty("field", out var fieldProp))
        {
            var fieldRef = fieldProp.GetString()!;

            // 'present' operator does not need the value.
            if (condition.TryGetProperty("present", out var presentProp))
            {
                bool exists = target.Fields.ContainsKey(fieldRef);
                return exists == presentProp.GetBoolean();
            }

            target.Fields.TryGetValue(fieldRef, out var raw);
            fieldValue = raw?.ToString();
        }

        if (fieldValue is null)
        {
            return false;
        }

        // Evaluate the operator.
        if (condition.TryGetProperty("equals", out var equalsProp))
        {
            return fieldValue.Equals(equalsProp.GetString(), StringComparison.Ordinal);
        }

        if (condition.TryGetProperty("notEquals", out var neqProp))
        {
            return !fieldValue.Equals(neqProp.GetString(), StringComparison.Ordinal);
        }

        if (condition.TryGetProperty("in", out var inProp))
        {
            var allowed = inProp.EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal);
            return allowed.Contains(fieldValue);
        }

        if (condition.TryGetProperty("notIn", out var notInProp))
        {
            var excluded = notInProp.EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal);
            return !excluded.Contains(fieldValue);
        }

        if (condition.TryGetProperty("matches", out var matchesProp))
        {
            var pattern = matchesProp.GetString()!;
            return Regex.IsMatch(fieldValue, pattern);
        }

        if (condition.TryGetProperty("satisfies", out var satisfiesProp))
        {
            var predicateName = satisfiesProp.GetString()!;

            if (!_predicates.TryGet(predicateName, out var predicate) || predicate is null)
            {
                return false; // Unknown predicate — conservative: fail.
            }

            return predicate.Evaluate(fieldValue, target);
        }

        // Unknown operator.
        return false;
    }

    /// <summary>Resolves a context key from the processing context or the root instance.</summary>
    private static string? ResolveContextValue(
        string key,
        ProcessingContext? context,
        GroupInstance instance)
    {
        if (key == "senderRole")
        {
            if (context?.SenderRoleOverride is not null)
            {
                return context.SenderRoleOverride;
            }

            // Fall back to the __senderRole stored on the root by DtcProcessor.
            var root = FindRoot(instance);
            root.Fields.TryGetValue("__senderRole", out var v);
            return v?.ToString();
        }

        // Support arbitrary Extra keys.
        if (context?.Extra?.TryGetValue(key, out var extra) == true)
        {
            return extra;
        }

        return null;
    }

    // ---- Tree traversal helpers ----

    /// <summary>Collects all instances with the given group code, depth-first.</summary>
    private static List<GroupInstance> CollectInstances(GroupInstance root, string code)
    {
        var results = new List<GroupInstance>();
        Traverse(root, code, results);
        return results;
    }

    private static void Traverse(GroupInstance node, string code, List<GroupInstance> results)
    {
        if (node.GroupCode == code)
        {
            results.Add(node);
        }

        foreach (var child in node.Children)
        {
            Traverse(child, code, results);
        }
    }

    private static GroupInstance FindRoot(GroupInstance instance)
    {
        var current = instance;
        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        return current;
    }
}

