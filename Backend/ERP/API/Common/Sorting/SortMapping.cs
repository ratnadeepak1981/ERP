using System.Linq.Expressions;

namespace API.Common.Sorting;

public class SortMapping<T>
{
    private readonly Dictionary<string, Expression<Func<T, object>>> _expressions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<T, object>> _compiled = new(StringComparer.OrdinalIgnoreCase);
    private string? _defaultProperty;
    private bool _defaultDescending;
    private Expression<Func<T, object>>? _secondaryExpression;
    private Func<T, object>? _secondaryCompiled;
    private bool _secondaryDescending;

    public IReadOnlyCollection<string> AllowedFields => _expressions.Keys;

    public SortMapping<T> Add(string propertyName, Expression<Func<T, object>> keySelector, bool isDefault = false, bool defaultDescending = false)
    {
        _expressions[propertyName] = keySelector;
        _compiled[propertyName] = keySelector.Compile();

        if (isDefault)
        {
            _defaultProperty = propertyName;
            _defaultDescending = defaultDescending;
        }

        return this;
    }

    public SortMapping<T> SetSecondary(Expression<Func<T, object>> keySelector, bool descending = false)
    {
        _secondaryExpression = keySelector;
        _secondaryCompiled = keySelector.Compile();
        _secondaryDescending = descending;
        return this;
    }

    public bool Validate(string? sortBy, string? sortDirection, out string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(sortDirection) &&
            !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = $"Invalid sort direction '{sortDirection}'. Allowed values are 'asc' or 'desc'.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sortBy) && !_expressions.ContainsKey(sortBy))
        {
            var allowed = string.Join(", ", _expressions.Keys);
            errorMessage = $"Invalid sort field '{sortBy}'. Allowed fields are: {allowed}.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public IQueryable<T> Apply(IQueryable<T> query, string? sortBy, string? sortDirection)
    {
        string? activeSort = !string.IsNullOrWhiteSpace(sortBy) && _expressions.ContainsKey(sortBy)
            ? sortBy
            : _defaultProperty;

        if (activeSort == null || !_expressions.TryGetValue(activeSort, out var selector))
        {
            return query;
        }

        bool isDesc = !string.IsNullOrWhiteSpace(sortBy)
            ? string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            : _defaultDescending;

        var ordered = isDesc ? query.OrderByDescending(selector) : query.OrderBy(selector);

        if (_secondaryExpression != null)
        {
            ordered = _secondaryDescending
                ? ordered.ThenByDescending(_secondaryExpression)
                : ordered.ThenBy(_secondaryExpression);
        }

        return ordered;
    }

    public IEnumerable<T> Apply(IEnumerable<T> source, string? sortBy, string? sortDirection)
    {
        string? activeSort = !string.IsNullOrWhiteSpace(sortBy) && _compiled.ContainsKey(sortBy)
            ? sortBy
            : _defaultProperty;

        if (activeSort == null || !_compiled.TryGetValue(activeSort, out var selector))
        {
            return source;
        }

        bool isDesc = !string.IsNullOrWhiteSpace(sortBy)
            ? string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            : _defaultDescending;

        var ordered = isDesc ? source.OrderByDescending(selector) : source.OrderBy(selector);

        if (_secondaryCompiled != null)
        {
            ordered = _secondaryDescending
                ? ordered.ThenByDescending(_secondaryCompiled)
                : ordered.ThenBy(_secondaryCompiled);
        }

        return ordered;
    }
}
