using System.Text.RegularExpressions;

namespace CodeTutor.Infrastructure.Ai;

public static partial class SecretRedactor
{
  [GeneratedRegex(@"(sk-[A-Za-z0-9_-]{8,})")]
  private static partial Regex ApiKeyPattern();

  public static string Redact(string? text)
  {
    if (string.IsNullOrEmpty(text))
      return string.Empty;

    var redacted = ApiKeyPattern().Replace(text, "sk-***");
    redacted = redacted.Replace("Bearer ", "Bearer ***", StringComparison.OrdinalIgnoreCase);
    return redacted;
  }
}
