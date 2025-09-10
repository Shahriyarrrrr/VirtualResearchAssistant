using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
using System.Threading.Tasks;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace VirtualResearchAssistant.Services
{
    public sealed class ExportService
    {
        static ExportService()
        {
            // Required by QuestPDF (Community license)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task SaveAsPdfAsync(string title, string content, string path)
        {
            // Run on background thread
            await Task.Run(() =>
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(36);
                        page.Size(PageSizes.A4);
                        page.Header()
                            .Text(title)
                            .SemiBold().FontSize(18).FontColor(Colors.Blue.Medium);

                        page.Content().Column(col =>
                        {
                            foreach (var line in SplitLines(content))
                                col.Item().Text(line).FontSize(11);
                        });

                        page.Footer().AlignRight().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                });

                doc.GeneratePdf(path);
            });
        }

        public async Task SaveAsWordAsync(string title, string content, string path)
        {
            await Task.Run(() =>
            {
                using var wordDoc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
                var main = wordDoc.AddMainDocumentPart();
                main.Document = new Word.Document(new Word.Body());

                var body = main.Document.Body!;
                // Title
                var titlePara = new Word.Paragraph(
                    new Word.Run(new Word.Text(title)));
                titlePara.ParagraphProperties = new Word.ParagraphProperties(
                    new Word.ParagraphStyleId { Val = "Heading1" });
                body.Append(titlePara);

                // Content
                foreach (var line in SplitLines(content))
                {
                    var p = new Word.Paragraph(new Word.Run(new Word.Text(line)));
                    body.Append(p);
                }

                main.Document.Save();
            });
        }

        private static IEnumerable<string> SplitLines(string s)
        {
            using var reader = new StringReader(s ?? "");
            string? line;
            while ((line = reader.ReadLine()) != null)
                yield return line;
        }
    }
}
