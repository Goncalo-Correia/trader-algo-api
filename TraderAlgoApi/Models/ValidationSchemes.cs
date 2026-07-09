namespace TraderAlgoApi.Models;

/// <summary>
/// Allow-list and normalization for the high-level ML <c>validation_scheme</c> forwarded to the
/// Python sidecar's <c>/train</c> endpoint. Values are the exact lowercase strings the sidecar
/// accepts (<c>single</c>, <c>block</c>, <c>sliding</c>); detailed fold/window knobs remain
/// engine-owned defaults in Python and are intentionally not modelled here.
/// </summary>
public static class ValidationSchemes
{
    /// <summary>Single chronological train/out-of-sample split (the default and fastest option).</summary>
    public const string Single = "single";

    /// <summary>Block walk-forward: equal blocks over the development region.</summary>
    public const string Block = "block";

    /// <summary>Sliding walk-forward: calendar windows simulating periodic retraining.</summary>
    public const string Sliding = "sliding";

    public static readonly IReadOnlySet<string> Allowed =
        new HashSet<string>(StringComparer.Ordinal) { Single, Block, Sliding };

    /// <summary>
    /// Normalizes a caller-supplied value: null/blank → <see cref="Single"/>; otherwise trimmed and
    /// lowercased. Does not validate — check the result with <see cref="IsValid"/>.
    /// </summary>
    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Single : value.Trim().ToLowerInvariant();

    public static bool IsValid(string value) => Allowed.Contains(value);
}
