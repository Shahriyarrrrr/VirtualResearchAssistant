using System.Linq;
using System.Text;

namespace StudentResearchAssistant.Services;

public class RagService
{
    public static string BuildContextFromHits((string path, int chunk, string snippet, float score)[] hits, int maxChars = 2800)
    {
        var sb = new StringBuilder();
        foreach (var h in hits.OrderByDescending(h => h.score))
        {
            var header = $"[DOC: {System.IO.Path.GetFileName(h.path)} | chunk {h.chunk} | score {h.score:F2}]\n";
            var text = h.snippet;
            if (sb.Length + header.Length + text.Length + 2 > maxChars) break;
            sb.AppendLine(header);
            sb.AppendLine(text);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
