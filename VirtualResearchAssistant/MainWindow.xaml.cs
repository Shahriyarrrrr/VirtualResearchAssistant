// Aliases to avoid ambiguity
using Microsoft.Win32;
// QuestPDF (PDF)
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using StudentResearchAssistant.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
// ✅ direct reference to your PdfService
using VirtualResearchAssistant.Services;
using DOP = DocumentFormat.OpenXml.Packaging;        // packaging alias
using W = DocumentFormat.OpenXml.Wordprocessing;     // wordprocessing alias

namespace VirtualResearchAssistant
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // ========== Status / Binding ==========
        private string _statusText = "Ready";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }

        // ========= History =========
        public ObservableCollection<HistoryItem> History { get; } = new();

        // ========= PDFs (names for UI + full paths we keep) =========
        private readonly ObservableCollection<string> _pdfListItems = new();
        private readonly List<string> _pdfFullPaths = new();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            if (FindName("ListHistory") is ListBox lbHistory)
                lbHistory.ItemsSource = History;

            if (FindName("ListPdfs") is ListBox lbPdfs)
                lbPdfs.ItemsSource = _pdfListItems;
        }

        #region Small helpers

        private async Task SetBusyAsync(string message, Func<Task> work)
        {
            try
            {
                IsBusy = true;
                StatusText = message;
                await work();
            }
            catch (Exception ex)
            {
                StatusText = "Error";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                StatusText = "Ready";
            }
        }

        private void AddHistory(string action, string prompt, string result)
        {
            History.Insert(0, new HistoryItem
            {
                Timestamp = DateTime.Now,
                Action = action,
                Prompt = prompt,
                Result = result
            });
        }

        private int GetSelectedPdfIndex()
        {
            if (FindName("ListPdfs") is ListBox lb)
                return lb.SelectedIndex;
            return -1;
        }

        private string? GetSelectedPdfPath()
        {
            var idx = GetSelectedPdfIndex();
            if (idx >= 0 && idx < _pdfFullPaths.Count)
                return _pdfFullPaths[idx];
            return null;
        }

        // Use PdfService directly to get text from the selected PDF
        private string GetSelectedPdfTextOrEmpty()
        {
            var path = GetSelectedPdfPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return string.Empty;

            try
            {
                return PdfService.ExtractText(path) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion

        #region Buttons

        private async void BtnLoadPdf_Click(object sender, RoutedEventArgs e)
        {
            await SetBusyAsync("Selecting PDFs…", async () =>
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Select PDF files",
                    Filter = "PDF files (*.pdf)|*.pdf",
                    Multiselect = true
                };

                if (dlg.ShowDialog() != true)
                    return;

                _pdfListItems.Clear();
                _pdfFullPaths.Clear();

                foreach (var path in dlg.FileNames)
                {
                    _pdfFullPaths.Add(path);
                    _pdfListItems.Add(System.IO.Path.GetFileName(path));
                }

                // ✅ default select first item so actions work immediately
                if (FindName("ListPdfs") is ListBox lb && _pdfListItems.Count > 0)
                    lb.SelectedIndex = 0;

                StatusText = _pdfFullPaths.Count == 0 ? "No PDFs selected" : $"Loaded {_pdfFullPaths.Count} PDF(s)";
                await Task.CompletedTask;
            });
        }

        private async void BtnSummarize_Click(object sender, RoutedEventArgs e)
        {
            await SetBusyAsync("Generating summary…", async () =>
            {
                var text = GetSelectedPdfTextOrEmpty();
                TxtOutput.Text = string.IsNullOrWhiteSpace(text)
                    ? "No PDF is selected or the PDF contains no extractable text."
                    : SummarizeText(text, maxSentences: 8);

                AddHistory("Summarize", TxtQuestion.Text, TxtOutput.Text);
                await Task.CompletedTask;
            });
        }

        private async void BtnKeywords_Click(object sender, RoutedEventArgs e)
        {
            await SetBusyAsync("Extracting keywords…", async () =>
            {
                var text = GetSelectedPdfTextOrEmpty();
                TxtOutput.Text = string.IsNullOrWhiteSpace(text)
                    ? "No PDF is selected or the PDF contains no extractable text."
                    : "### Keywords\n" + string.Join(", ", ExtractKeywords(text, 20));

                AddHistory("Keywords", TxtQuestion.Text, TxtOutput.Text);
                await Task.CompletedTask;
            });
        }

        private async void BtnQuiz_Click(object sender, RoutedEventArgs e)
        {
            await SetBusyAsync("Generating quiz…", async () =>
            {
                var text = GetSelectedPdfTextOrEmpty();
                TxtOutput.Text = string.IsNullOrWhiteSpace(text)
                    ? "No PDF is selected or the PDF contains no extractable text."
                    : GenerateSimpleQuiz(text, 5);

                AddHistory("Quiz", TxtQuestion.Text, TxtOutput.Text);
                await Task.CompletedTask;
            });
        }

        private async void BtnAsk_Click(object sender, RoutedEventArgs e)
        {
            await SetBusyAsync("Answering…", async () =>
            {
                var text = GetSelectedPdfTextOrEmpty();
                var question = (TxtQuestion.Text ?? "").Trim();

                if (string.IsNullOrWhiteSpace(text))
                    TxtOutput.Text = "No PDF is selected or the PDF contains no extractable text.";
                else if (string.IsNullOrWhiteSpace(question))
                    TxtOutput.Text = "Type a question first.";
                else
                {
                    var answer = LocalAnswer(text, question);
                    TxtOutput.Text = string.IsNullOrWhiteSpace(answer)
                        ? "I couldn't find a direct answer in the PDF text."
                        : answer;
                }

                AddHistory("Ask", question, TxtOutput.Text);
                await Task.CompletedTask;
            });
        }

        #endregion

        #region History actions

        private void BtnHistoryClear_Click(object sender, RoutedEventArgs e)
        {
            if (History.Any() &&
                MessageBox.Show("Clear history?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                History.Clear();
                StatusText = "History cleared";
            }
        }

        private void BtnHistoryCopyLast_Click(object sender, RoutedEventArgs e)
        {
            var last = History.FirstOrDefault();
            if (last != null)
            {
                Clipboard.SetText(last.Result ?? string.Empty);
                StatusText = "Copied last result";
            }
            else
            {
                StatusText = "History is empty";
            }
        }

        #endregion

        #region Export (PDF / Word)

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var text = TxtOutput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Nothing to export. Generate some content first.", "Export PDF",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "PDF file|*.pdf",
                FileName = "VRA-Export.pdf"
            };
            if (sfd.ShowDialog() != true) return;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().Text("Virtual Research Assistant").SemiBold().FontSize(16);
                    page.Content().Text(text).FontSize(11);
                    page.Footer().AlignRight().Text($"Generated: {DateTime.Now:g}").FontSize(9);
                });
            }).GeneratePdf(sfd.FileName);

            StatusText = "PDF exported";
        }

        private void BtnExportWord_Click(object sender, RoutedEventArgs e)
        {
            var text = TxtOutput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Nothing to export. Generate some content first.", "Export Word",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "Word document|*.docx",
                FileName = "VRA-Export.docx"
            };
            if (sfd.ShowDialog() != true) return;

            using var doc = DOP.WordprocessingDocument.Create(
                sfd.FileName,
                DocumentFormat.OpenXml.WordprocessingDocumentType.Document
            );

            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new W.Document(new W.Body());
            var body = mainPart.Document.Body!;

            var titlePara = new W.Paragraph(new W.Run(new W.Text("Virtual Research Assistant")));
            titlePara.ParagraphProperties = new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = "Heading1" });
            body.Append(titlePara);

            foreach (var line in text.Replace("\r", "").Split('\n'))
                body.Append(new W.Paragraph(new W.Run(new W.Text(line ?? ""))));

            mainPart.Document.Save();
            StatusText = "Word exported";
        }

        #endregion

        #region Lightweight NLP helpers

        private static string SummarizeText(string text, int maxSentences = 8)
        {
            var sentences = Regex
                .Split(text, @"(?<=[\.!\?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(maxSentences);

            return "### Summary\n" + string.Join(" ", sentences);
        }

        private static readonly HashSet<string> _stopwords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the","and","for","you","are","but","not","with","from","this","that","have","was","were","has","had",
            "into","out","our","your","about","after","before","over","under","as","at","on","in","of","to","a","an",
            "it","its","by","is","be","or","if","then","than","can","may","might","should","would","could","will",
            "we","they","them","he","she","his","her","their"
        };

        private static IEnumerable<string> ExtractKeywords(string text, int topN = 20)
        {
            var tokens = Regex
                .Matches(text.ToLowerInvariant(), @"[a-z]{3,}")
                .Select(m => m.Value)
                .Where(w => !_stopwords.Contains(w));

            return tokens
                .GroupBy(w => w)
                .OrderByDescending(g => g.Count())
                .Take(topN)
                .Select(g => g.Key);
        }

        private static string GenerateSimpleQuiz(string text, int questions = 5)
        {
            var sentences = Regex
                .Split(text, @"(?<=[\.!\?])\s+")
                .Where(s => s.Length > 30)
                .Take(200)
                .ToList();

            var rnd = new Random();
            var selected = sentences.OrderBy(_ => rnd.Next()).Take(questions).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("### Multiple-Choice Questions (MCQs)\n");

            int qn = 1;
            foreach (var s in selected)
            {
                var blanks = s;
                var word = Regex.Matches(s, @"\b[A-Za-z]{6,}\b")
                                .Cast<Match>()
                                .FirstOrDefault()?.Value;
                if (!string.IsNullOrWhiteSpace(word))
                    blanks = s.Replace(word, "_____");

                sb.AppendLine($"{qn}. {blanks}");
                sb.AppendLine($"   A) {word}");
                sb.AppendLine($"   B) (distractor)");
                sb.AppendLine($"   C) (distractor)");
                sb.AppendLine($"   D) (distractor)");
                sb.AppendLine($"   **Answer:** A");
                sb.AppendLine();
                qn++;
            }
            return sb.ToString();
        }

        private static string LocalAnswer(string text, string question)
        {
            var qWords = Regex
                .Matches(question.ToLowerInvariant(), @"[a-z]{3,}")
                .Select(m => m.Value)
                .Where(w => !_stopwords.Contains(w))
                .Distinct()
                .ToHashSet();

            var sentences = Regex
                .Split(text, @"(?<=[\.!\?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s));

            var hits = sentences
                .Select(s => new
                {
                    Sentence = s,
                    Score = qWords.Count(w => s.ToLowerInvariant().Contains(w))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(6)
                .Select(x => x.Sentence);

            return string.Join(" ", hits);
        }

        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }

    public class HistoryItem
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string Result { get; set; } = "";
        public override string ToString() => $"[{Timestamp:t}] {Action}: {Prompt}";
    }
}
