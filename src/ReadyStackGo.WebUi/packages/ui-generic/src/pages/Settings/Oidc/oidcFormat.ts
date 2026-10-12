// Date formatting of the single sign-on settings pages.

export function formatDate(value: string): string {
  return new Date(value).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
}

/**
 * The provider list's last result (frame 173:3055): "Sign-in failed · 5 min ago",
 * "Test passed · Oct 2, 2026", and a failed test by its reason ("Issuer does not match").
 */
export function lastResultLabel(
  result: { kind: string; passed: boolean; at: string; message?: string | null },
  now: Date = new Date(),
): string {
  const when = relativeTime(result.at, now);
  if (result.kind === "signIn") {
    return result.passed ? `Sign-in passed · ${when}` : `Sign-in failed · ${when}`;
  }
  return result.passed ? `Test passed · ${when}` : result.message || `Test failed · ${when}`;
}

/** "2 min ago", "3 h ago", otherwise the date. */
export function relativeTime(value: string, now: Date = new Date()): string {
  const seconds = Math.max(0, Math.round((now.getTime() - new Date(value).getTime()) / 1000));
  if (seconds < 60) return "just now";
  if (seconds < 3600) return `${Math.floor(seconds / 60)} min ago`;
  if (seconds < 86400) return `${Math.floor(seconds / 3600)} h ago`;
  return formatDate(value);
}
