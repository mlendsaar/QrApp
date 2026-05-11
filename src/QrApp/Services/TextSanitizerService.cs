using System.Text.RegularExpressions;

namespace QrApp;

internal sealed class TextSanitizerService
{
    private readonly IReadOnlyList<(string match, string replace, Regex? regex)> _rules;

    public TextSanitizerService(IEnumerable<SanitizerRule> rules)
    {
        _rules = rules.Select(r => (
            r.Match,
            r.Replace,
            r.IsRegex ? new Regex(r.Match, RegexOptions.Compiled | RegexOptions.Multiline) : null
        )).ToList();
    }

    public string Sanitize(string input)
    {
        foreach (var (match, replace, regex) in _rules)
            input = regex is not null
                ? regex.Replace(input, replace)
                : input.Replace(match, replace, StringComparison.Ordinal);
        return input.Trim();
    }
}
