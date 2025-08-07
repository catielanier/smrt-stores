using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public class SlugService 
{
  public static string GenerateSlug(string phrase)
  {
    string normalized = phrase.Normalize(System.Text.NormalizationForm.FormD);
    var sb = new StringBuilder();
    foreach (var c in normalized)
    {
      var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
      if (unicodeCategory != UnicodeCategory.NonSpacingMark)
        sb.Append(c);
    }
    string cleaned = sb.ToString().Normalize(NormalizationForm.FormC);

    cleaned = cleaned.ToLowerInvariant();

    cleaned = Regex.Replace(cleaned, @"[^a-z0-9\s-]", "");

    cleaned = Regex.Replace(cleaned, @"[\s-]+", "-").Trim('-');

    return cleaned;
  }
}