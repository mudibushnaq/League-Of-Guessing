using System.Text;

public static class StringUtils
{
    public static string NormalizeAnswer(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.ToLowerInvariant();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (char.IsLetter(ch)) sb.Append(ch);
        return RemoveDiacritics(sb.ToString());
    }

    public static string RemoveDiacritics(string s)
    {
        var formD = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (char ch in formD)
        {
            var uc = char.GetUnicodeCategory(ch);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}