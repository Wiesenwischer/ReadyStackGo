namespace ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// SemVer 2.0.0 precedence comparison + pre-release "channel" extraction.
///
/// Replaces naive string/<see cref="System.Version"/> comparisons that mis-ordered pre-release
/// versions (e.g. treating <c>4.0.0-preview.1</c> as newer than <c>4.0.0-ci</c> by plain
/// alphabetical compare, or a pre-release as newer than its final release).
/// </summary>
public static class SemanticVersion
{
    /// <summary>
    /// Compares two version strings by SemVer 2.0.0 precedence. Returns &lt;0 if
    /// <paramref name="a"/> precedes <paramref name="b"/>, 0 if equal precedence, &gt;0 otherwise.
    /// Leading <c>v</c> and build metadata (<c>+...</c>) are ignored. Non-SemVer inputs fall back
    /// to an ordinal string comparison.
    /// </summary>
    public static int Compare(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b)) return 0;
        if (string.IsNullOrWhiteSpace(a)) return -1;
        if (string.IsNullOrWhiteSpace(b)) return 1;

        var (aBase, aPre, aOk) = Split(a);
        var (bBase, bPre, bOk) = Split(b);

        // If either side is not parseable as SemVer base, fall back to ordinal compare.
        if (!aOk || !bOk)
            return string.Compare(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

        // 1. Compare numeric base (major.minor.patch...).
        var baseCmp = CompareBase(aBase, bBase);
        if (baseCmp != 0) return baseCmp;

        // 2. A version WITH a pre-release has LOWER precedence than the same version without.
        var aHasPre = aPre.Length > 0;
        var bHasPre = bPre.Length > 0;
        if (!aHasPre && !bHasPre) return 0;
        if (!aHasPre) return 1;   // a is the final release → greater
        if (!bHasPre) return -1;  // b is the final release → greater

        // 3. Compare pre-release identifiers left to right.
        return ComparePreRelease(aPre, bPre);
    }

    /// <summary>True if <paramref name="candidate"/> has strictly higher precedence than <paramref name="current"/>.</summary>
    public static bool IsNewer(string? candidate, string? current) => Compare(candidate, current) > 0;

    /// <summary>
    /// Extracts the release "channel" from a version: the pre-release stream a build belongs to.
    /// <c>4.0.0-ci</c> → <c>ci</c>; <c>4.0.0-preview.1</c> → <c>preview</c>; <c>4.0.0-rc.2</c> → <c>rc</c>;
    /// <c>4.0.0-beta3</c> → <c>beta</c>; a final release (<c>4.0.0</c>) → <c>""</c> (stable).
    /// Used so an update is only offered within the same channel (no ci → preview jumps).
    /// </summary>
    public static string Channel(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return string.Empty;
        var (_, pre, _) = Split(version);
        if (pre.Length == 0) return string.Empty; // stable / final release

        var first = pre[0];                         // first dot-separated identifier
        var letters = new string(first.TakeWhile(c => !char.IsDigit(c)).ToArray());
        return (letters.Length > 0 ? letters : first).ToLowerInvariant();
    }

    /// <summary>True if both versions are on the same release channel.</summary>
    public static bool SameChannel(string? a, string? b)
        => string.Equals(Channel(a), Channel(b), StringComparison.OrdinalIgnoreCase);

    // ── internals ─────────────────────────────────────────────────────

    private static string Normalize(string v)
    {
        v = v.Trim().TrimStart('v', 'V');
        var plus = v.IndexOf('+');           // strip build metadata
        return plus >= 0 ? v[..plus] : v;
    }

    private static (int[] Base, string[] Pre, bool Ok) Split(string version)
    {
        var v = Normalize(version);

        var dash = v.IndexOf('-');
        var basePart = dash >= 0 ? v[..dash] : v;
        var prePart = dash >= 0 ? v[(dash + 1)..] : string.Empty;

        var baseTokens = basePart.Split('.');
        var nums = new int[baseTokens.Length];
        for (var i = 0; i < baseTokens.Length; i++)
        {
            if (!int.TryParse(baseTokens[i], out nums[i]))
                return (Array.Empty<int>(), Array.Empty<string>(), false);
        }

        var pre = prePart.Length == 0
            ? Array.Empty<string>()
            : prePart.Split('.');

        return (nums, pre, true);
    }

    private static int CompareBase(int[] a, int[] b)
    {
        var len = Math.Max(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            var av = i < a.Length ? a[i] : 0;
            var bv = i < b.Length ? b[i] : 0;
            if (av != bv) return av.CompareTo(bv);
        }
        return 0;
    }

    private static int ComparePreRelease(string[] a, string[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            var aNum = int.TryParse(a[i], out var an);
            var bNum = int.TryParse(b[i], out var bn);

            if (aNum && bNum)
            {
                if (an != bn) return an.CompareTo(bn);
            }
            else if (aNum != bNum)
            {
                // Numeric identifiers always have LOWER precedence than alphanumeric ones.
                return aNum ? -1 : 1;
            }
            else
            {
                var c = string.Compare(a[i], b[i], StringComparison.Ordinal);
                if (c != 0) return c;
            }
        }

        // All shared identifiers equal → more identifiers wins.
        return a.Length.CompareTo(b.Length);
    }
}
