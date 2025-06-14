using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

public static class MarkdownParser
{
    public static ObservableCollection<Inline> Parse(string content)
    {
        var inlines = new ObservableCollection<Inline>();

        if (string.IsNullOrEmpty(content))
            return inlines;

        var pattern = @"(\*\*(.*?)\*\*|\*(.*?)\*)";
        var matches = Regex.Matches(content, pattern);

        int lastIndex = 0;

        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
            {
                string plain = content.Substring(lastIndex, match.Index - lastIndex);
                inlines.Add(new Run(plain));
            }

            if (match.Value.StartsWith("**"))
            {
                string boldText = match.Groups[2].Value;
                inlines.Add(new Run(boldText) { FontWeight = FontWeights.Bold });
            }
            else if (match.Value.StartsWith("*"))
            {
                string italicText = match.Groups[3].Value;
                inlines.Add(new Run(italicText) { FontStyle = FontStyles.Italic });
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < content.Length)
        {
            inlines.Add(new Run(content.Substring(lastIndex)));
        }

        return inlines;
    }
}