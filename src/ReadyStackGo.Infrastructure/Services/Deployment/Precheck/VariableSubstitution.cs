using System.Text.RegularExpressions;

namespace ReadyStackGo.Infrastructure.Services.Deployment.Precheck;

/// <summary>
/// Resolves ${VARIABLE} and ${VARIABLE:-default} placeholders in strings
/// using a provided variable dictionary. Used by precheck rules to resolve
/// template strings (ports, images, volumes, networks) before validation.
/// </summary>
internal static partial class VariableSubstitution
{
    [GeneratedRegex(@"\$\{([A-Za-z_][A-Za-z0-9_]*)(:-([^}]*))?\}", RegexOptions.Compiled)]
    private static partial Regex VariablePattern();

    /// <summary>
    /// Resolves variable placeholders in the input string.
    /// Unresolved variables (no value, no default) are left as-is.
    /// </summary>
    internal static string Resolve(string input, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains("${"))
            return input;

        return VariablePattern().Replace(input, match =>
        {
            var varName = match.Groups[1].Value;
            var hasDefault = match.Groups[3].Success;
            var defaultValue = hasDefault ? match.Groups[3].Value : null;

            if (variables.TryGetValue(varName, out var value))
                return value;
            if (defaultValue != null)
                return defaultValue;
            return match.Value; // Leave unresolved
        });
    }
}
