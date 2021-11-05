using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace Split
{
    public class MainWindowViewModel : ViewModelBase
    {
        // Default filename pattern: {Auftragsnummer}_{Produktnummer}_{Format}_{Ausrichtung}_S{Index}_Bog{Bogenanzahl}

        /*
         * Formate:
         * 350x500
         * 320x450
         * 478x
         *
         * Ausrichtung:
         * hoch
         * quer
         */
        private static MainWindowViewModel instance;

        private string fileNamePattern = "";

        private string format;

        private string jobNumber;

        private string orientation;

        private bool processRunning;

        private string productNumber;

        private TrulyObservableCollection<ResultingFile> resultingFiles;

        private string selectedOutputDirectory;

        private PdfDocument selectedPdf;

        private int splitAfter = 1;

        private bool zipDocuments;

        private string zipFileNamePattern;

        public MainWindowViewModel()
        {
            OnWindowLoaded += InitWindowLoaded;

            OpenPdfCommand = new RelayCommand(OpenPdfDialog);
            ProcessPdfCommand = new RelayCommand(ProcessPdf, () => { return !string.IsNullOrEmpty(SelectedOutputDirectory) && SelectedPdf != null && !processRunning; });
            SelectOutputDirectoryCommand = new RelayCommand(SelectOutputDirectory);

            SelectedOutputDirectory = Properties.Settings.Default.OutputDirectory;
            ZipDocuments = Properties.Settings.Default.ZipDocuments;
            FileNamePattern = Properties.Settings.Default.FileNamePattern;
            ZipFileNamePattern = Properties.Settings.Default.ZipFileNamePattern;
        }

        public static MainWindowViewModel Instance
        {
            get
            {
                if (instance is null)
                {
                    instance = new MainWindowViewModel();
                }

                return instance;
            }
        }

        public string FileNamePattern
        {
            get { return fileNamePattern; }
            set
            {
                fileNamePattern = value;
                Properties.Settings.Default.FileNamePattern = FileNamePattern;
                Properties.Settings.Default.Save();
                FilenameChanged();
                RaisePropertyChanged();
            }
        }

        public string FileNamePatternTooltip
        {
            get
            {
                string tooltip = "Verfügbare Tags:" + Environment.NewLine;
                foreach (var pair in PatternValuePairs)
                {
                    tooltip += Environment.NewLine + pair.Key;
                }
                return tooltip;
            }
        }

        public string Format
        {
            get { return format; }
            set
            {
                format = value;
                FilenameChanged();
                RaisePropertyChanged();
            }
        }

        public string JobNumber
        {
            get { return jobNumber; }
            set
            {
                jobNumber = value;
                FilenameChanged();
                RaisePropertyChanged();
            }
        }

        public Action OnWindowLoaded { get; private set; }

        public RelayCommand OpenPdfCommand { get; private set; }

        public string Orientation
        {
            get { return orientation; }
            set
            {
                orientation = value;
                FilenameChanged();
                RaisePropertyChanged();
            }
        }

        public Dictionary<string, string> PatternValuePairs
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "{Auftragsnummer}", JobNumber },
                    { "{Produktnummer}", ProductNumber },
                    { "{Format}", Format },
                    { "{Ausrichtung}", Orientation },
                    { "{Index}", "" },
                    { "{Bogenanzahl}", "" },
                };
            }
        }

        public RelayCommand ProcessPdfCommand { get; private set; }

        public string ProductNumber
        {
            get { return productNumber; }
            set
            {
                productNumber = value;
                FilenameChanged();
                RaisePropertyChanged();
            }
        }

        public TrulyObservableCollection<ResultingFile> ResultingFiles
        {
            get { return resultingFiles; }
            set
            {
                resultingFiles = value;
                RaisePropertyChanged();
            }
        }

        public string SelectedOutputDirectory
        {
            get { return selectedOutputDirectory; }
            set
            {
                selectedOutputDirectory = value;
                Properties.Settings.Default.OutputDirectory = selectedOutputDirectory;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public PdfDocument SelectedPdf
        {
            get { return selectedPdf; }
            set
            {
                selectedPdf = value;
                UpdateResultingFiles();
                RaisePropertyChanged();
                ProcessPdfCommand.RaiseCanExecuteChanged();
            }
        }

        public RelayCommand SelectOutputDirectoryCommand { get; private set; }

        public int SplitAfter
        {
            get { return splitAfter; }
            set
            {
                splitAfter = value;
                UpdateResultingFiles();
                RaisePropertyChanged();
            }
        }

        public bool ZipDocuments
        {
            get { return zipDocuments; }
            set
            {
                zipDocuments = value;
                Properties.Settings.Default.ZipDocuments = zipDocuments;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string ZipFileNamePattern
        {
            get { return zipFileNamePattern; }
            set
            {
                zipFileNamePattern = value;
                Properties.Settings.Default.ZipFileNamePattern = zipFileNamePattern;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public void CheckForUpdate()
        {
            new TaskFactory().StartNew(() =>
            {
                var version = typeof(MainWindowViewModel).Assembly.GetName().Version;
                string downloadUrl = "https://github.com/schmarxman/split-pdf/releases/latest";

                WebClient wc = new WebClient();
                wc.Headers.Add("User-Agent: Other");

                string json = "";
                try
                {
                    json = wc.DownloadString("https://api.github.com/repos/schmarxman/split-pdf/releases/latest");
                }
                catch (Exception)
                {
                }

                if (!string.IsNullOrEmpty(json))
                {
                    var tag = json.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(x => x.Contains("tag_name"));
                    if (!string.IsNullOrEmpty(tag))
                    {
                        int start = tag.IndexOf("v") + 1;
                        var remoteVersion = new Version(tag.Substring(start, tag.LastIndexOf("\"") - start));
                        if (remoteVersion > version)
                        {
                            if (MessageBox.Show("Eine neue Version ist verfügbar. Jetzt herunterladen? (Öffnet den Browser)", "Update verfügbar", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                            {
                                Process.Start(downloadUrl);
                            }
                        }
                    }
                }
            });
        }

        public string GetFilenameFromPattern(string pattern, string bogenCount, int index)
        {
            string filename = pattern;

            var pairs = PatternValuePairs;
            pairs["{Bogenanzahl}"] = bogenCount;
            pairs["{Index}"] = "" + index;

            foreach (var pair in pairs)
            {
                filename = filename.Replace(pair.Key, pair.Value);
            }

            return filename;
        }

        public void OpenPdf(string fileName)
        {
            FileInfo info = null;
            try
            {
                SelectedPdf = PdfReader.Open(fileName, PdfDocumentOpenMode.Import);
                info = new FileInfo(fileName);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error opening file {fileName}: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (info != null)
            {
                var name = info.Name.Replace(info.Extension, "");
                if (name.Contains("_"))
                {
                    var splitName = name.Split('_');
                    try
                    {
                        JobNumber = splitName[0];
                        ProductNumber = splitName[1];
                        Format = splitName[2];
                        Orientation = splitName[3];
                    }
                    catch (Exception)
                    {
                        // Name does not contain JobNumber and/or ProductNumber.
                    }
                }

                SelectedOutputDirectory = info.DirectoryName;
            }
        }

        private void FilenameChanged()
        {
            if (ResultingFiles != null)
            {
                foreach (var item in ResultingFiles)
                {
                    item.NotifyPropertyChanged(nameof(ResultingFile.FileName));
                }
            }
        }

        private void InitWindowLoaded()
        {
            CheckForUpdate();
        }
        private void OpenPdfDialog()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                //Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
                Filter = "PDF files (*.pdf)|*.pdf",
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    OpenPdf(openFileDialog.FileName);
                }
                catch (Exception)
                {
                    // todo error handling
                }
            }
        }

        private void ProcessPdf()
        {
            processRunning = true;

            var splitDocs = SplitDocument(SelectedPdf, SplitAfter);

            if (ZipDocuments)
            {
                var zipName = Path.Combine(SelectedOutputDirectory, GetFilenameFromPattern(ZipFileNamePattern, "0", 0) + ".zip");
                SaveDocumentsToZip(splitDocs, zipName);
            }
            else
            {
                SaveDocumentsToFiles(splitDocs, SelectedOutputDirectory);
            }

            processRunning = false;
        }

        private void SaveDocumentsToFiles(Dictionary<PdfDocument, string> docs, string directory)
        {
            foreach (var pair in docs)
            {
                var fileName = Path.Combine(directory, pair.Value);
                try
                {
                    pair.Key.Save(fileName);
                }
                catch (Exception)
                {
                    // TODO error handling
                }
            }
        }

        private void SaveDocumentsToZip(Dictionary<PdfDocument, string> docs, string filename)
        {
            File.Delete(filename);
            using (var zip = ZipFile.Open(filename, ZipArchiveMode.Create))
            {
                foreach (var pair in docs)
                {
                    var entry = zip.CreateEntry(pair.Value, CompressionLevel.Fastest);
                    using (var entryStream = entry.Open())
                    {
                        pair.Key.Save(entryStream);
                    }
                }
            }
        }

        private void SelectOutputDirectory()
        {
            var selectDirDialog = new CommonOpenFileDialog()
            {
                IsFolderPicker = true,
                RestoreDirectory = true
            };

            if (selectDirDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                SelectedOutputDirectory = selectDirDialog.FileName;
            }
        }

        private Dictionary<PdfDocument, string> SplitDocument(PdfDocument sourceDoc, int splitAfter = 1)
        {
            var result = new Dictionary<PdfDocument, string>();
            int pageIndex = 0;
            int resultIndex = 0;

            while (pageIndex < sourceDoc.PageCount)
            {
                var tempDoc = new PdfDocument();

                for (int i = 0; i < splitAfter; i++)
                {
                    if (pageIndex < sourceDoc.PageCount)
                    {
                        tempDoc.AddPage(sourceDoc.Pages[pageIndex]);
                    }
                    pageIndex++;
                }

                result.Add(tempDoc, ResultingFiles[resultIndex].FileName);
                resultIndex++;
            }

            return result;
        }

        private void UpdateResultingFiles()
        {
            var files = new TrulyObservableCollection<ResultingFile>();

            if (SelectedPdf != null)
            {
                int fileCount = (int)Math.Ceiling((float)SelectedPdf.PageCount / SplitAfter);
                for (int i = 0; i < fileCount; i++)
                {
                    files.Add(new ResultingFile(i + 1));
                }
            }

            ResultingFiles = files;
        }
    }

    public class ResultingFile : INotifyPropertyChanged
    {
        private string bogenCount = "1";

        private MathExpressionParser MathParser = new MathExpressionParser();

        public ResultingFile(int index)
        {
            Index = index;
            MathParser.OnParseSucceeded += (result) => { return (float)Math.Ceiling(result); };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string BogenCount
        {
            get { return bogenCount; }
            set
            {
                bogenCount = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(FileName));
            }
        }

        public string FileName
        {
            get
            {
                return MainWindowViewModel.Instance.GetFilenameFromPattern(MainWindowViewModel.Instance.FileNamePattern, bogenCount, Index) + ".pdf";
            }
        }

        public int Index { get; private set; }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        internal void EvaluateBogenCount()
        {
            float result;
            if (MathParser.Parse(BogenCount, out result))
            {
                BogenCount = "" + result;
            }
        }
    }
}