using DRMS_OCRToolkit.Models;
using Ghostscript.NET.Rasterizer;
using Google.Cloud.Vision.V1;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DRMS_OCRToolkit
{
    public class OCRTool
    {
        private readonly string _credentialsPath;
        private readonly ImageAnnotatorClient _client;
        private Regex _regex = new Regex(@"[^a-zA-Z0-9 -]");
        private readonly string _connectionString;

        public OCRTool(string credentialsPath, string connString)
        {
            _credentialsPath = credentialsPath;
            ImageAnnotatorClientBuilder clientBuiler =
                new ImageAnnotatorClientBuilder { CredentialsPath = credentialsPath };
            _client = clientBuiler.Build();

            _connectionString = connString;
        }

        #region Getters/Setters
        public string GetConnectionString()
        {
            return string.Copy(_connectionString);
        }

        public OCRTool SetConnectionString(string connString)
        {
            using (var test = new DataModel(connString)) { }
            return new OCRTool(_credentialsPath, connString);
        }

        public string GetCredentials()
        {
            return string.Copy(_credentialsPath);
        }

        public OCRTool SetCredentials(string credentialsPath)
        {
            return new OCRTool(credentialsPath, _connectionString);
        }
        #endregion

        #region Helpers
        private void ClearFiles(string[] imagePaths)
        {
            foreach (var file in imagePaths)
            {
                File.Delete(file);
            }
        }

        public List<PageText> ProcessResponse(TextAnnotation response, string docID, int pageNum)
        {
            var results = new List<PageText>();
            foreach (var page in response.Pages)
            {
                foreach (var block in page.Blocks)
                {
                    foreach (var paragraph in block.Paragraphs)
                    {
                        foreach (var word in paragraph.Words)
                        {
                            var text = _regex.Replace(string.Join("", word.Symbols.Select(s => s.Text)), string.Empty);
                            results.Add(new PageText
                            {
                                DocumentID = docID,
                                PageNumber = pageNum,
                                Text = text,
                                ULX = word.BoundingBox.Vertices[0].X,
                                ULY = word.BoundingBox.Vertices[0].Y,
                                LRX = word.BoundingBox.Vertices[2].X,
                                LRY = word.BoundingBox.Vertices[2].Y
                            });
                        }
                    }
                }
            }
            return results;
        }

        private void ValidateVision()
        {
            if (string.IsNullOrEmpty(_credentialsPath))
                throw new ArgumentException("You must set the credentials before reading a pdf.");
        }
                
        private static string[] GetPngImage(string psFilename, string outputPath, int dpi = 300)
        {
            using (var rasterizer = new GhostscriptRasterizer()) //create an instance for GhostscriptRasterizer
            {
                rasterizer.Open(psFilename); //opens the PDF file for rasterizing

                PdfReader reader = new PdfReader(psFilename);
                var results = new string[reader.NumberOfPages];
                for (int pageNum = 0; pageNum < reader.NumberOfPages; pageNum++)
                {
                    //set the output image(png's) complete path
                    var outputPNGPath = Path.Combine(outputPath, $"{Path.GetFileNameWithoutExtension(psFilename).Trim()}_page{pageNum + 1}.png");
                    results[pageNum] = outputPNGPath;

                    //converts the PDF pages to png's 
                    var pdf2PNG = rasterizer.GetPage(dpi, pageNum + 1);

                    //save the png's
                    pdf2PNG.Save(outputPNGPath, System.Drawing.Imaging.ImageFormat.Png);
                }
                return results;
            }
        }

        private string[] ProcessPaths(string[] filePaths, bool overrideExisting)
        {
            using (var context = new DataModel(_connectionString))
            {
                var fileNames = filePaths.Select(s => Path.GetFileNameWithoutExtension(s).Trim()).ToArray();
                if (overrideExisting)
                {
                    var prevDocs = context.Documents.Where(f => fileNames.Contains(f.FileName));
                    var prevText = context.PageText.Where(f => prevDocs.Select(s => s.FileName).Contains(f.DocumentID));
                    //Remove previous PageText of Documents from DB
                    context.PageText.RemoveRange(prevText);
                    //Remove previous Documents from DB
                    context.Documents.RemoveRange(prevDocs);
                    context.SaveChanges();
                    return filePaths;
                }
                else
                {
                    var matchedKeys = context.Documents.Where(f => fileNames.Contains(f.FileName)).Select(s => s.FileName);
                    fileNames = fileNames.Where(s => !matchedKeys.Contains(s)).ToArray();
                    return filePaths.Where(s => fileNames.Contains(Path.GetFileNameWithoutExtension(s).Trim())).ToArray();
                }
            }
        }
        #endregion

        #region Writing
        public async Task WriteToDBAsync(string[] filePaths, bool overrideExisting)
        {
            ValidateVision();

            var toProcess = ProcessPaths(filePaths, overrideExisting);
            if (toProcess.Length > 0)
            {
                var tempPath = Path.GetTempPath();
                var pngBlock = new BufferBlock<Tuple<string, string[]>>();
                var dbBlock = new BufferBlock<Tuple<Document, List<PageText>>>();

                var post = Task.Run(() =>
                {
                    foreach (var path in toProcess)
                    {
                        pngBlock.Post(new Tuple<string, string[]>
                            (Path.GetFileNameWithoutExtension(path).Trim(), GetPngImage(path, tempPath)));
                    }
                    pngBlock.Complete();
                });
                var receiveThenPost = Task.Run(() =>
                {
                    while (!pngBlock.Completion.IsCompleted)
                    {
                        var tuple = pngBlock.Receive();
                        var document = new Document { FileName = tuple.Item1.Trim() };
                        var docText = new List<PageText>();
                        Parallel.For(0, tuple.Item2.Length, pageNum =>
                        {
                            var image = Image.FromFile(tuple.Item2[pageNum]);
                            File.Delete(tuple.Item2[pageNum]); //Cleanup
                            var response = _client.DetectDocumentText(image);
                            docText.AddRange(ProcessResponse(response, document.FileName, pageNum));
                        });
                        dbBlock.Post(new Tuple<Document, List<PageText>>(document, docText));
                    }
                    dbBlock.Complete();
                });
                var receive = Task.Run(() =>
                {
                    while (!dbBlock.Completion.IsCompleted)
                    {
                        var tuple = dbBlock.Receive();
                        using (var context = new DataModel(_connectionString))
                        {
                            context.Documents.Add(tuple.Item1);
                            context.PageText.AddRange(tuple.Item2);
                            context.SaveChanges();
                        }
                    }
                });

                await Task.WhenAll(post, receiveThenPost, receive);
            }
            else
                return;
        }

        public void WriteToDB(string filePath, bool overrideExisting)
        {
            ValidateVision();

            var toProcess = ProcessPaths(new string[] { filePath }, overrideExisting);
            if (toProcess.Length > 0)
            {
                var imagePaths = GetPngImage(toProcess[0], Path.GetTempPath());
                var document = new Document { FileName = Path.GetFileNameWithoutExtension(toProcess[0]).Trim() };
                var docText = new List<PageText>();
                Parallel.For(0, imagePaths.Length, pageNum =>
                {
                    var image = Image.FromFile(imagePaths[pageNum]);
                    File.Delete(imagePaths[pageNum]); //Cleanup
                    var response = _client.DetectDocumentText(image);
                    docText.AddRange(ProcessResponse(response, document.FileName, pageNum));
                });
                using (var context = new DataModel(_connectionString))
                {
                    context.Documents.Add(document);
                    context.PageText.AddRange(docText);
                    context.SaveChanges();
                }
            }
        }
        public async Task WriteToDBAsync(string filePath, bool overrideExisting)
        {
            await Task.Run(() => WriteToDB(filePath, overrideExisting));
        }

        public void WriteToDB(string filePath, int pageNum, bool overrideExisting)
        {
            ValidateVision();

            var toProcess = ProcessPaths(new string[] { filePath }, overrideExisting);
            if (toProcess.Length > 0)
            {
                var imagePaths = GetPngImage(toProcess[0], Path.GetTempPath());
                var document = new Document { FileName = Path.GetFileNameWithoutExtension(toProcess[0]).Trim() };
                var docText = new List<PageText>();
                var image = Image.FromFile(imagePaths[pageNum]);
                var response = _client.DetectDocumentText(image);
                docText.AddRange(ProcessResponse(response, document.FileName, pageNum));
                using (var context = new DataModel(_connectionString))
                {
                    context.Documents.Add(document);
                    context.PageText.AddRange(docText);
                    context.SaveChanges();
                }
                ClearFiles(imagePaths); //Cleanup
            }
        }
        public async Task WriteToDBAsync(string filePath, int pageNum, bool overrideExisting)
        {
            await Task.Run(() => WriteToDB(filePath, pageNum, overrideExisting));
        }
        #endregion

        #region Searching
        public SortedList<string, List<string>> FindDocuments(string[] searchWords)
        {
            Parallel.For(0, searchWords.Length, i =>
            {
                searchWords[i] = searchWords[i].Trim().ToUpper();
            });

            var results = new SortedList<string, List<string>>();
            List<PageText> pages;
            using (var context = new DataModel(_connectionString))
            {
                pages = context.PageText.Where(s => searchWords.Contains(s.Text.ToUpper())).ToList();
            }
            foreach (var page in pages)
            {
                if (results.ContainsKey(page.DocumentID))
                {
                    if (!results[page.DocumentID].Contains(page.Text))
                    {
                        results[page.DocumentID].Add(page.Text);
                    }
                }
                else
                {
                    results.Add(page.DocumentID, new List<string> { page.Text });
                }
            }
            return results;
        }
        public async Task<SortedList<string, List<string>>> FindDocumentsAsync(string[] searchWords)
        {
            return await Task.Run(() => FindDocuments(searchWords));
        }

        public SortedList<string, List<string>> FindDocuments(string[] searchWords, string[] searchDocs)
        {
            Parallel.For(0, searchWords.Length, i =>
            {
                searchWords[i] = searchWords[i].Trim().ToUpper();
            });                

            var results = new SortedList<string, List<string>>();
            List<PageText> pages;
            using (var context = new DataModel(_connectionString))
            {
                pages = context.PageText.Where(s => searchDocs.Contains(s.DocumentID) && searchWords.Contains(s.Text.ToUpper())).ToList();
            }
            foreach (var page in pages)
            {
                if (results.ContainsKey(page.DocumentID))
                {
                    if (!results[page.DocumentID].Contains(page.Text))
                    {
                        results[page.DocumentID].Add(page.Text);
                    }
                }
                else
                {
                    results.Add(page.DocumentID, new List<string> { page.Text });
                }
            }
            return results;
        }
        public async Task<SortedList<string, List<string>>> FindDocumentsAsync(string[] searchWords, string[] searchDocs)
        {
            return await Task.Run(() => FindDocuments(searchWords, searchDocs));
        }
        #endregion

        #region Reading
        public bool DocumentExists(string filePath)
        {
            using(var context = new DataModel(_connectionString))
            {
                var docID = Path.GetFileNameWithoutExtension(filePath).Trim();
                return context.Documents.Any(s => s.FileName == docID);
            }
        }

        public bool DocumentContains(bool dbSearch, string filePath, string[] searchWords)
        {
            Parallel.For(0, searchWords.Length, i =>
            {
                searchWords[i] = searchWords[i].ToUpper();
            });
            if (dbSearch && DocumentExists(filePath))
            {
                using (var context = new DataModel(_connectionString))
                {
                    var docID = Path.GetFileNameWithoutExtension(filePath).Trim();
                    return context.PageText.Any(s => 
                        s.DocumentID == docID
                        && searchWords.Contains(s.Text.ToUpper()));
                }
            }
            else
            {
                ValidateVision();

                var imagePaths = GetPngImage(filePath, Path.GetTempPath());
                bool result = false;
                Parallel.ForEach(imagePaths, path =>
                {
                    var image = Image.FromFile(path);
                    var response = _client.DetectDocumentText(image);
                    foreach (var page in response.Pages)
                    {
                        foreach (var block in page.Blocks)
                        {
                            foreach (var paragraph in block.Paragraphs)
                            {
                                foreach (var word in paragraph.Words)
                                {
                                    if (searchWords.Contains(string.Join("", word.Symbols.Select(s => s.Text)).ToUpper()))
                                    {
                                        result = true;
                                        return;
                                    }
                                }
                            }
                        }
                    }
                });
                return result;
            }
        }
        public async Task<bool> DocumentContainsAsync(bool dbSearch, string filePath, string[] searchWords)
        {
            return await Task.Run(() => DocumentContains(dbSearch, filePath, searchWords));
        }

        public string[] GetDocumentText(bool dbSearch, string filePath)
        {
            if (dbSearch && DocumentExists(filePath))
            {
                using (var context = new DataModel(_connectionString))
                {
                    var docID = Path.GetFileNameWithoutExtension(filePath).Trim();
                    if (!context.Documents.Any(s => s.FileName == docID))
                        return new string[0];

                    var allText = context.PageText.Where(s => s.DocumentID == docID)
                        .OrderBy(s => s.ID).ToList()
                        .GroupBy(s => s.PageNumber)
                        .Select(s => string.Join(" ", s.Select(f => f.Text)))
                        .ToList();
                    return allText.ToArray();
                }
            }
            else
            {
                ValidateVision();

                var imagePaths = GetPngImage(filePath, Path.GetTempPath());
                var result = new List<string>();
                Parallel.ForEach(imagePaths, path =>
                {
                    var image = Image.FromFile(path);
                    var response = _client.DetectDocumentText(image);
                    result.Add(response.Text.Trim());
                });
                return result.ToArray();
            }
        }
        public async Task<string[]> GetDocumentTextAsync(bool dbSearch, string filePath)
        {
            return await Task.Run(() => GetDocumentText(dbSearch, filePath));
        }
        #endregion
    }
}
