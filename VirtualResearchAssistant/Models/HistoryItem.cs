namespace VirtualResearchAssistant.Models
{
    public sealed class HistoryItem
    {
        public DateTime When { get; set; } = DateTime.Now;
        public string Query { get; set; } = "";
        public string ResultPreview { get; set; } = "";

        public override string ToString()
            => $"{When:HH:mm} – {Query}  ·  {Trim(ResultPreview)}";

        private static string Trim(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= 80 ? s : s[..80] + "…";
        }
    }
}
