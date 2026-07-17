namespace TraderAlgoApi.Models;

/// <summary>
/// Allow-list and normalization for the high-level ML <c>validation_scheme</c> forwarded to the
/// Python sidecar's <c>/train</c> endpoint. Values are the exact lowercase strings the sidecar
/// accepts (<c>single</c>, <c>block</c>); detailed fold/window knobs remain engine-owned defaults
/// in Python and are intentionally not modelled here. The retired <c>sliding</c> scheme is coerced
/// to <c>block</c> on normalization (see <see cref="Normalize"/>).
/// </summary>
public static class ValidationSchemes
{
    /// <summary>Single chronological train/out-of-sample split (the default and fastest option).</summary>
    public const string Single = "single";

    /// <summary>Block walk-forward: equal blocks over the development region.</summary>
    public const string Block = "block";

    public static readonly IReadOnlySet<string> Allowed =
        new HashSet<string>(StringComparer.Ordinal) { Single, Block };

    /// <summary>
    /// Normalizes a caller-supplied value: null/blank → <see cref="Single"/>; otherwise trimmed and
    /// lowercased. The retired <c>sliding</c> scheme (which the sidecar now rejects with a 422) is
    /// coerced to <see cref="Block"/> — its closest surviving walk-forward equivalent — so legacy
    /// policies keep training instead of failing. Check the result with <see cref="IsValid"/>.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Single;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized == "sliding" ? Block : normalized;
    }

    public static bool IsValid(string value) => Allowed.Contains(value);
}
