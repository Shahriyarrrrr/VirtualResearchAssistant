using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Highlight;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using Directory = Lucene.Net.Store.Directory;

namespace StudentResearchAssistant.Services
{
    public class IndexService : IDisposable
    {
        private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
        private readonly Analyzer _analyzer = new StandardAnalyzer(AppLuceneVersion);
        private readonly Directory _dir;
        private readonly IndexWriter _writer;

        public IndexService(string indexPath)
        {
            System.IO.Directory.CreateDirectory(indexPath);
            _dir = FSDirectory.Open(indexPath);

            var config = new IndexWriterConfig(AppLuceneVersion, _analyzer)
            {
                // Fully-qualify OpenMode to avoid VB namespace clashes
                OpenMode = Lucene.Net.Index.OpenMode.CREATE_OR_APPEND,
                Similarity = new BM25Similarity()
            };

            _writer = new IndexWriter(_dir, config);
        }

        public void AddOrUpdateChunks(string pdfPath, IEnumerable<(int index, string chunk)> chunks)
        {
            _writer.DeleteDocuments(new Term("path", pdfPath));
            foreach (var (i, text) in chunks)
            {
                var doc = new Document
                {
                    new StringField("path", pdfPath, Field.Store.YES),
                    new Int32Field("chunk", i, Field.Store.YES),
                    new TextField("content", text, Field.Store.YES),
                };
                _writer.AddDocument(doc);
            }
        }

        public void Commit() => _writer.Commit();

        public IEnumerable<(string path, int chunk, string snippet, float score)> Search(string query, int topK = 6)
        {
            using var reader = _writer.GetReader(true);
            var searcher = new IndexSearcher(reader) { Similarity = new BM25Similarity() };

            var parser = new QueryParser(AppLuceneVersion, "content", _analyzer);
            Query luceneQuery;
            try { luceneQuery = parser.Parse(QueryParser.Escape(query)); }
            catch { luceneQuery = parser.Parse(query); }

            var hits = searcher.Search(luceneQuery, topK).ScoreDocs;

            var scorer = new QueryScorer(luceneQuery, "content");
            var formatter = new SimpleHTMLFormatter("[[", "]]");
            var highlighter = new Highlighter(formatter, scorer)
            {
                TextFragmenter = new SimpleFragmenter(160)
            };

            foreach (var hit in hits)
            {
                var doc = searcher.Doc(hit.Doc);
                var text = doc.Get("content");
                var tokenStream = _analyzer.GetTokenStream("content", text);
                var frag = highlighter.GetBestFragments(tokenStream, text, 2, " … ");
                if (string.IsNullOrWhiteSpace(frag)) frag = text[..Math.Min(text.Length, 240)];

                yield return (doc.Get("path"), int.Parse(doc.Get("chunk")), frag, hit.Score);
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _dir?.Dispose();
            _analyzer?.Dispose();
            GC.SuppressFinalize(this); // quiets CA1816
        }
    }
}
