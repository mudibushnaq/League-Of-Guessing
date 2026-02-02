using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;

public interface IProgressionService
{
    // Set/replace rules at runtime (so you can compute conditions with injected services)
    void Configure(IEnumerable<ModeRule> rules);

    bool IsUnlocked(string modeKey);
    string GetLockedHint(string modeKey); // user-facing description
    void NotifyStateMaybeChanged();       // call when something that affects locks may have changed
    event Action OnProgressionChanged;    // UI can subscribe
}

// A mode is unlocked if: unlockedByDefault || ALL conditions evaluate to true.
public sealed class ModeRule
{
    public string ModeKey;
    public bool UnlockedByDefault;
    public List<(Func<bool> cond, Func<string> describe)> Conditions = new();
}

[SingletonClass(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.Project,
    extraBindings: typeof(IProgressionService))]
public sealed class ProgressionService : IProgressionService, IProjectInitializable
{
    int IProjectInitializable.Order => 0;
    
    readonly Dictionary<string, ModeRule> _rules = new(StringComparer.OrdinalIgnoreCase);

    public event Action OnProgressionChanged;
    
    UniTask IProjectInitializable.Initialize() => UniTask.CompletedTask;
    
    public void Configure(IEnumerable<ModeRule> rules)
    {
        _rules.Clear();
        foreach (var r in rules) _rules[r.ModeKey] = r;
        OnProgressionChanged?.Invoke();
    }

    public bool IsUnlocked(string modeKey)
    {
        if (!_rules.TryGetValue(modeKey, out var r)) return true; // no rule => unlocked
        if (r.UnlockedByDefault) return true;
        if (r.Conditions == null || r.Conditions.Count == 0) return false;
        return r.Conditions.All(c => SafeEval(c.cond));
    }

    public string GetLockedHint(string modeKey)
    {
        if (!_rules.TryGetValue(modeKey, out var r)) return "";
        if (r.UnlockedByDefault || r.Conditions == null || r.Conditions.Count == 0) return "";
        var unmet = r.Conditions.Where(c => !SafeEval(c.cond)).Select(c => SafeDescribe(c.describe));
        return string.Join("\n", unmet);
    }

    public void NotifyStateMaybeChanged() => OnProgressionChanged?.Invoke();

    static bool SafeEval(Func<bool> f) { try { return f?.Invoke() ?? false; } catch { return false; } }
    static string SafeDescribe(Func<string> f) { try { return f?.Invoke() ?? ""; } catch { return ""; } }
}