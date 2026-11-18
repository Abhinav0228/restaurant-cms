using RestaurantCms.Domain.Common;

namespace RestaurantCms.Application.Common;

/// <summary>Checks the shape of a request (required fields, lengths, ranges) before its handler runs.
/// Business rules stay in the domain; this gives clients precise, per-field messages.</summary>
public interface IValidator<in T>
{
    IReadOnlyDictionary<string, string[]> Validate(T instance);
}

/// <summary>Small, dependency-free validator base: declare rules in the constructor.</summary>
public abstract class Validator<T> : IValidator<T>
{
    private readonly List<(string Field, Func<T, bool> IsValid, string Message)> _rules = [];
    private readonly List<Func<T, IReadOnlyDictionary<string, string[]>>> _includes = [];

    /// <summary>Validates a nested object with another validator and merges its field errors.</summary>
    protected void Include<TChild>(Func<T, TChild> child, IValidator<TChild> validator) =>
        _includes.Add(x => child(x) is { } value ? validator.Validate(value) : new Dictionary<string, string[]>());

    protected void Rule(string field, Func<T, bool> isValid, string message) => _rules.Add((field, isValid, message));

    protected void Required(string field, Func<T, string?> value, string? message = null) =>
        Rule(field, x => !string.IsNullOrWhiteSpace(value(x)), message ?? $"{Label(field)} is required.");

    protected void MaxLength(string field, Func<T, string?> value, int max) =>
        Rule(field, x => (value(x)?.Trim().Length ?? 0) <= max, $"{Label(field)} must be {max} characters or fewer.");

    protected void Range(string field, Func<T, decimal> value, decimal min, decimal max) =>
        Rule(field, x => value(x) >= min && value(x) <= max, $"{Label(field)} must be between {min} and {max}.");

    protected void NotEmpty(string field, Func<T, Guid> value) =>
        Rule(field, x => value(x) != Guid.Empty, $"{Label(field)} is required.");

    public IReadOnlyDictionary<string, string[]> Validate(T instance)
    {
        var errors = new Dictionary<string, List<string>>();
        foreach (var (field, isValid, message) in _rules)
        {
            bool ok;
            try { ok = isValid(instance); } catch (NullReferenceException) { ok = false; }
            if (ok) continue;
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
            list.Add(message);
        }
        foreach (var include in _includes)
            foreach (var (field, messages) in include(instance))
            {
                if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
                list.AddRange(messages);
            }
        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }

    private static string Label(string field) =>
        field.Length == 0 ? field : char.ToUpperInvariant(field[0]) + System.Text.RegularExpressions.Regex.Replace(field[1..], "([A-Z])", " $1").ToLowerInvariant();
}
