using System.Collections.Generic;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;

namespace StudentResearchAssistant.Services;

public static class PdfService
{
    public static string ExtractText(string pdfPath)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(pdfPath);
        foreach (var page in doc.GetPages())
        {
            sb.AppendLine(page.Text);
            sb.AppendLine();
        }
        return Normalize(sb.ToString());
    }

    public static IEnumerable<(int index, string chunk)> ChunkText(string text, int maxChars = 1200, int overlap = 120)
    {
        var words = text.Split(' ', '\n', '\r', '\t')
                        .Where(w => !string.IsNullOrWhiteSpace(w))
                        .ToArray();

        var current = new StringBuilder();
        int idx = 0, chunkIndex = 0;
        while (idx < words.Length)
        {
            if (current.Length + words[idx].Length + 1 > maxChars)
            {
                yield return (chunkIndex++, current.ToString());
                int backWords = overlap / 6;
                idx = System.Math.Max(0, idx - backWords);
                current.Clear();
            }
            current.Append(words[idx]).Append(' ');
            idx++;
        }
        if (current.Length > 0) yield return (chunkIndex, current.ToString());
    }

    private static string Normalize(string s) =>
        s.Replace("\r", " ").Replace("\n", " ").Replace("  ", " ").Trim();
}
