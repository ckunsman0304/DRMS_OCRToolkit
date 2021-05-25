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
        private readonly Regex _regex = new Regex(@"[^a-zA-Z0-9 -]");
        private readonly string _connectionString;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="credentialsPath">Path to the credentials JSON file for 
        /// access to Google VisionAI.</param>
        /// <param name="connString">Connection string used to connect to 
        /// desired database.</param>
        public OCRTool(string credentialsPath, string connString)
        {
            _credentialsPath = credentialsPath;
            ImageAnnotatorClientBuilder clientBuiler =
                new ImageAnnotatorClientBuilder { CredentialsPath = credentialsPath };
            _client = clientBuiler.Build();

            _connectionString = connString;
        }

        #region Getters/Setters
        /// <summary>
        /// Getter method for the connection string.
        /// </summary>
        /// <returns>A copy of the connection string.</returns>
        public string GetConnectionString()
        {
            return string.Copy(_connectionString);
        }

        /// <summary>
        /// Setter method that returns a new OCRTool object with 
        /// the new connection string set.
        /// </summary>
        /// <param name="connString">The new connection string.</param>
        /// <returns>A new OCRTool object.</returns>
        public OCRTool SetConnectionString(string connString)
        {
            using (var test = new DataModel(connString)) { }
            return new OCRTool(_credentialsPath, connString);
        }

        /// <summary>
        /// Getter method for the credentials path.
        /// </summary>
        /// <returns>A copy of the credentials path.</returns>
        public string GetCredentials()
        {
            return string.Copy(_credentialsPath);
        }

        /// <summary>
        /// Setter method that returns a new OCRTool object with the 
        /// specified credentials path set.
        /// </summary>
        /// <param name="credentialsPath">New credentials path.</param>
        /// <returns>A new OCRTool object.</returns>
        public OCRTool SetCredentials(string credentialsPath)
        {
            return new OCRTool(credentialsPath, _connectionString);
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Deletes all given files.
        /// </summary>
        /// <param name="imagePaths">Paths of files to be deleted.</param>
        private void ClearFiles(string[] imagePaths)
        {
            foreach (var file in imagePaths)
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Turns Google VisionAI response into a list of PageText objects.
        /// </summary>
        /// <param name="response">Response from Google VisionAI</param>
        /// <param name="docID">The file name of the document that was scanned.</param>
        /// <param name="pageNum">Specific page number that was scanned (0-based index).</param>
        /// <returns></returns>
        private List<PageText> ProcessResponse(TextAnnotation response, string docID, int pageNum)
        {
            var results = new List<PageText>();
            foreach (var page in response.Pages)
            {
                foreach (var block in page.Blocks)
                {
                    foreach (var paragraph in block.Paragraphs)
                    {
                        Parallel.ForEach(paragraph.Words, word =>
                        {
                            var text = _regex.Replace(string.Join("", word.Symbols.Select(s => s.Text)), string.Empty);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                results.Add(new PageText
                                {
                                    DocumentID = docID,
                                    PageNumber = pageNum,
                                    Text = text,
                                    //Coordinates as percentages of the page
                                    Left = (word.BoundingBox.Vertices[0].X < word.BoundingBox.Vertices[3].X 
                                          ? word.BoundingBox.Vertices[0].X 
                                          : word.BoundingBox.Vertices[3].X) / (decimal)page.Width,

                                    Top = (word.BoundingBox.Vertices[0].Y < word.BoundingBox.Vertices[1].Y 
                                         ? word.BoundingBox.Vertices[0].Y 
                                         : word.BoundingBox.Vertices[1].Y) / (decimal)page.Height,

                                    Right = (word.BoundingBox.Vertices[2].X > word.BoundingBox.Vertices[1].X 
                                           ? word.BoundingBox.Vertices[2].X 
                                           : word.BoundingBox.Vertices[1].X) / (decimal)page.Width,

                                    Bottom = (word.BoundingBox.Vertices[2].Y > word.BoundingBox.Vertices[3].Y 
                                            ? word.BoundingBox.Vertices[2].Y 
                                            : word.BoundingBox.Vertices[3].Y) / (decimal)page.Height
                                });
                            }
                        });
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Ensures that the Google VisionAI client has been built.
        /// Throws an ArgumentException if not.
        /// </summary>
        private void ValidateVision()
        {
            if (string.IsNullOrEmpty(_credentialsPath))
                throw new ArgumentException("You must set the credentials before reading a pdf.");
        }

        /// <summary>
        /// Turn each pdf page into it's own png file to be processed.
        /// </summary>
        /// <param name="psFilename">File path to the pdf.</param>
        /// <param name="outputPath">Path to dump the output into.</param>
        /// <param name="dpi"></param>
        /// <returns>Array where each object in the array is a file path to a png file.</returns>
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

        /// <summary>
        /// Processes the file paths. If overrideExisting is true it deletes 
        /// existing data from the database. Otherwise it removes that file 
        /// path from the array.
        /// </summary>
        /// <param name="filePaths">PDF file paths to process.</param>
        /// <param name="overrideExisting">Indicate whether or not you want to 
        /// override any existing data.</param>
        /// <returns>Fully processed array of file paths to use.</returns>
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
        /// <summary>
        /// Batch processing method for performing OCR on all pages of all 
        /// documents provided and writes the result to the database.
        /// </summary>
        /// <param name="filePaths">Array of paths to the pdf files to be added.</param>
        /// <param name="overrideExisting">Indicate whether or not to override existing 
        /// data for any of the documents (if there is any).</param>
        /// <returns>An awaitable Task object.</returns>
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

        /// <summary>
        /// Performs OCR on all pages of the provided document and
        /// writes the results to the database.
        /// </summary>
        /// <param name="filePath">Path to the pdf file to be added.</param>
        /// <param name="overrideExisting">Indicate whether or not to override 
        /// existing data for this document (if there is any).</param>
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
        /// <summary>
        /// Performs OCR on all pages of the provided document and
        /// writes the results to the database.
        /// </summary>
        /// <param name="filePath">Path to the pdf file to be added.</param>
        /// <param name="overrideExisting">Indicate whether or not to override 
        /// existing data for this page of the document (if there is any).</param>
        /// <returns>An awaitable Task object.</returns>
        public async Task WriteToDBAsync(string filePath, bool overrideExisting)
        {
            await Task.Run(() => WriteToDB(filePath, overrideExisting));
        }

        /// <summary>
        /// Performs OCR on the specified page of the provided document and 
        /// writes the result to the database.
        /// </summary>
        /// <param name="filePath">Path to the pdf file to be added.</param>
        /// <param name="pageNum">Page number to be scanned (0-based index).</param>
        /// <param name="overrideExisting">Indicate whether or not to override 
        /// existing data for this page of the document (if there is any).</param>
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
        /// <summary>
        /// Performs OCR on the specified page of the provided document and 
        /// writes the result to the database.
        /// </summary>
        /// <param name="filePath">Path to the pdf file to be added.</param>
        /// <param name="pageNum">Page number to be scanned (0-based index).</param>
        /// <param name="overrideExisting">Indicate whether or not to override 
        /// existing data for this page of the document (if there is any).</param>
        /// <returns>An awaitable Task object.</returns>
        public async Task WriteToDBAsync(string filePath, int pageNum, bool overrideExisting)
        {
            await Task.Run(() => WriteToDB(filePath, pageNum, overrideExisting));
        }

        /// <summary>
        /// If there is data in the database for the specified file then it is deleted.
        /// </summary>
        /// <param name="filePath">Path to the pdf file to delete data for.</param>
        public void DeleteFromDB(string filePath)
        {
            using (var context = new DataModel(_connectionString))
            {
                var docID = Path.GetFileNameWithoutExtension(filePath).Trim();
                if(context.PageText.Any(s => s.DocumentID == docID))
                    context.PageText.RemoveRange(context.PageText.Where(s => s.DocumentID == docID));
                if (context.Documents.Any(s => s.FileName == docID))
                    context.Documents.RemoveRange(context.Documents.Where(s => s.FileName == docID));
                context.SaveChanges();
            }
        }
        /// <summary>
        /// If there is data in the database for the specified file then it is deleted.
        /// </summary>
        /// <param name="filePath">Path to the pdf file to delete data for.</param>
        public async Task DeleteFromDBAsync(string filePath)
        {
            await Task.Run(() => DeleteFromDB(filePath));
        }
        #endregion

        #region Searching
        /// <summary>
        /// Searches through all documents in the database and returns the file name of documents that contain
        /// any of the given words.
        /// </summary>
        /// <param name="searchWords">Words to match (NOT case sensitive).</param>
        /// <returns>Sorted list where the key is the file name of a matched document and 
        /// the value is a list of matched words.</returns>
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
        /// <summary>
        /// Searches through all documents in the database and returns the file name of documents that contain
        /// any of the given words.
        /// </summary>
        /// <param name="searchWords">Words to match (NOT case sensitive).</param>
        /// <returns>Sorted list where the key is the file name of a matched document and 
        /// the value is a list of matched words.</returns>
        public async Task<SortedList<string, List<string>>> FindDocumentsAsync(string[] searchWords)
        {
            return await Task.Run(() => FindDocuments(searchWords));
        }

        /// <summary>
        /// Searches through the given documents and returns the file name of documents that contain
        /// any of the given words.
        /// </summary>
        /// <param name="searchWords">Words to match (NOT case sensitive).</param>
        /// <param name="searchDocs">File paths of documents to search through.</param>
        /// <returns>Sorted list where the key is the file name of a matched document and 
        /// the value is a list of matched words.</returns>
        public SortedList<string, List<string>> FindDocuments(string[] searchWords, string[] searchDocs)
        {
            Parallel.For(0, searchWords.Length, i =>
            {
                searchWords[i] = _regex.Replace(searchWords[i].ToUpper(), string.Empty);
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
        /// <summary>
        /// Searches through the given documents and returns the file name of documents that contain
        /// any of the given words.
        /// </summary>
        /// <param name="searchWords">Words to match (NOT case sensitive).</param>
        /// <param name="searchDocs">File paths of documents to search through.</param>
        /// <returns>Sorted list where the key is the file name of a matched document and 
        /// the value is a list of matched words.</returns>
        public async Task<SortedList<string, List<string>>> FindDocumentsAsync(string[] searchWords, string[] searchDocs)
        {
            return await Task.Run(() => FindDocuments(searchWords, searchDocs));
        }
        #endregion

        #region Reading
        /// <summary>
        /// Determines whether or not the database already has data for the provided document.
        /// </summary>
        /// <param name="filePath">Path to the pdf file that should be matched.</param>
        /// <returns>Value indicating if the database has data for this document.</returns>
        public bool DocumentExists(string filePath)
        {
            using(var context = new DataModel(_connectionString))
            {
                var docID = Path.GetFileNameWithoutExtension(filePath).Trim();
                return context.Documents.Any(s => s.FileName == docID);
            }
        }

        /// <summary>
        /// Determines whether or not the given document contains one or more of the words given in the array.
        /// If dbSearch is set to true then it will search the database for matching values first. If the 
        /// document does not exist in the database then it will scan the document again and use that to 
        /// determine if the document contains any of the words or not.
        /// </summary>
        /// <param name="dbSearch">Indicated whether you want to search the database for a match first.</param>
        /// <param name="filePath">Path to the pdf file that should be searched.</param>
        /// <param name="searchWords">Words to match (NOT case sensitive).</param>
        /// <returns>Boolean indicating whether or not the document contains any of the provided words.</returns>
        public bool DocumentContains(bool dbSearch, string filePath, string[] searchWords)
        {
            Parallel.For(0, searchWords.Length, i =>
            {
                searchWords[i] = _regex.Replace(searchWords[i].ToUpper(), string.Empty);
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
                                    var text = _regex.Replace(string.Join("", word.Symbols.Select(s => s.Text)).ToUpper(), string.Empty);
                                    if (searchWords.Contains(text))
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
        /// <summary>
        /// Determines whether or not the given document contains one or more of the words given in the array.
        /// If dbSearch is set to true then it will search the database for matching values first. If the 
        /// document does not exist in the database then it will scan the document again and use that to 
        /// determine if the document contains any of the words or not.
        /// </summary>
        /// <param name="dbSearch">Indicated whether you want to search the database for a match first.</param>
        /// <param name="filePath">Path to the pdf file that should be searched.</param>
        /// <param name="searchWords">Words to match (NOT case sensitive).</param>
        /// <returns>Boolean indicating whether or not the document contains any of the provided words.</returns>
        public async Task<bool> DocumentContainsAsync(bool dbSearch, string filePath, string[] searchWords)
        {
            return await Task.Run(() => DocumentContains(dbSearch, filePath, searchWords));
        }

        /// <summary>
        /// Provides the OCR text of the specified document. If dbSearch is set to true then it will search the
        /// database for matching values first. If the document does not exist in the database then it will scan
        /// the document again and provide that output.
        /// </summary>
        /// <param name="dbSearch">Indicate whether you want to search the database for a match first.</param>
        /// <param name="filePath">Path to the pdf file that should be processed.</param>
        /// <returns>Array where each object in the array is a string of that respective page's text.</returns>
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
                    result.Add(_regex.Replace(response.Text.Trim(), string.Empty));
                });
                return result.ToArray();
            }
        }
        /// <summary>
        /// Provides the OCR text of the specified document. If dbSearch is set to true then it will search the
        /// database for matching values first. If the document does not exist in the database then it will scan
        /// the document again and provide that output.
        /// </summary>
        /// <param name="dbSearch">Indicate whether you want to search the database for a match first.</param>
        /// <param name="filePath">Path to the pdf file that should be processed.</param>
        /// <returns>Array where each object in the array is a string of that respective page's text.</returns>
        public async Task<string[]> GetDocumentTextAsync(bool dbSearch, string filePath)
        {
            return await Task.Run(() => GetDocumentText(dbSearch, filePath));
        }
        #endregion

        #region Bounding Boxes
        /// <summary>
        /// Provides a list of the PageText objects for the specified document. If dbSearch is set to true then it will search the
        /// database for matching values first. If the document does not exist in the database then it will scan
        /// the document and use that output.
        /// </summary>
        /// <param name="dbSearch">Indicate whether you want to search the database for a match first.</param>
        /// <param name="filePath">Path to the pdf file that should be processed.</param>
        /// <returns>List of PageText objects for representing the document's text.</returns>
        public List<PageText> GetDocumentPageText(bool dbSearch, string filePath)
        {
            var docID = Path.GetFileNameWithoutExtension(filePath).Trim();
            if (dbSearch && DocumentExists(filePath))
            {
                using (var context = new DataModel(_connectionString))
                {
                    if (!context.Documents.Any(s => s.FileName == docID))
                        return new List<PageText>();

                    var allText = context.PageText.Where(s => s.DocumentID == docID)
                        .OrderBy(s => s.PageNumber)
                        .ThenBy(s => s.ID)
                        .ToList();
                    return allText;
                }
            }
            else
            {
                ValidateVision();

                var imagePaths = GetPngImage(filePath, Path.GetTempPath());
                var result = new List<PageText>();
                Parallel.For(0, imagePaths.Length, pageNum =>
                {
                    var image = Image.FromFile(imagePaths[pageNum]);
                    var response = _client.DetectDocumentText(image);
                    result.AddRange(ProcessResponse(response, docID, pageNum));
                });
                return result;
            }
        }
        /// <summary>
        /// Provides a list of the PageText objects for the specified document. If dbSearch is set to true then it will search the
        /// database for matching values first. If the document does not exist in the database then it will scan
        /// the document and use that output.
        /// </summary>
        /// <param name="dbSearch">Indicate whether you want to search the database for a match first.</param>
        /// <param name="filePath">Path to the pdf file that should be processed.</param>
        /// <returns>List of PageText objects for representing the document's text.</returns>
        public async Task<List<PageText>> GetDocumentPageTextAsync(bool dbSearch, string filePath)
        {
            return await Task.Run(() => GetDocumentPageText(dbSearch, filePath));
        }

        /// <summary>
        /// Searches through the given documents and returns the file name of documents that contain
        /// any of the given words.
        /// </summary>
        /// <param name="searchWords">Words to match (NOT case sensitive).</param>
        /// <param name="searchDocs">File paths of documents to search through.</param>
        /// <returns>List of PageText objects with matching criteria.</returns>
        public List<PageText> SearchDocuments(string[] searchWords, string[] searchDocs)
        {
            Parallel.For(0, searchWords.Length, i =>
            {
                searchWords[i] = _regex.Replace(searchWords[i].ToUpper(), string.Empty);
            });

            var results = new List<PageText>();
            using (var context = new DataModel(_connectionString))
            {
                results = context.PageText.Where(s => searchDocs.Contains(s.DocumentID) && searchWords.Contains(s.Text.ToUpper())).ToList();
            }
            return results;
        }
        /// <summary>
        /// Searches through the given documents and returns the file name of documents that contain
        /// any of the given words.
        /// </summary>
        /// <param name="searchWords">Words to match (NOT case sensitive).</param>
        /// <param name="searchDocs">File paths of documents to search through.</param>
        /// <returns>List of PageText objects with matching criteria.</returns>
        public async Task<List<PageText>> SearchDocumentsAsync(string[] searchWords, string[] searchDocs)
        {
            return await Task.Run(() => SearchDocuments(searchWords, searchDocs));
        }

        /// <summary>
        /// Searches through all documents in the database and returns the file name of documents that contain
        /// any of the given words.
        /// </summary>
        /// <param name="searchWords">Words to match (NOT case sensitive).</param>
        /// <returns>List of PageText objects with matching criteria.</returns>
        public List<PageText> SearchDocuments(string[] searchWords)
        {
            Parallel.For(0, searchWords.Length, i =>
            {
                searchWords[i] = searchWords[i].Trim().ToUpper();
            });

            var results = new List<PageText>();
            using (var context = new DataModel(_connectionString))
            {
                results = context.PageText.Where(s => searchWords.Contains(s.Text.ToUpper())).ToList();
            }
            return results;
        }
        /// <summary>
        /// Searches through all documents in the database and returns the file name of documents that contain
        /// any of the given words.
        /// </summary>
        /// <param name="searchWords">Words to match (NOT case sensitive).</param>
        /// <returns>List of PageText objects with matching criteria.</returns>
        public async Task<List<PageText>> SearchDocumentsAsync(string[] searchWords)
        {
            return await Task.Run(() => SearchDocuments(searchWords));
        }
        #endregion
    }
}
