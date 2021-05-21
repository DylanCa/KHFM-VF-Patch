﻿using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace KHFM_VF_Patch
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static readonly string PATCH_FOLDER = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "Resources/Patches");

        private const string DONATE_URL = "https://www.paypal.com/donate/?business=QB2DD2YWXZ79E&currency_code=EUR";

        private const string KH1_PATCH_VOICES_ZIP_NAME = "KH1FM-Voices.patch";
        private const string KH1_PATCH_VIDEOS_ZIP_NAME = "KH1FM-Videos.patch";
        private const string KH1_PATCH_TEXTURES_ZIP_NAME = "KH1FM-Textures.patch";
        private const string KH1_PATCH_MAGIC_ZIP_NAME = "KH1FM-Magic.patch";

        private const string KH1_PATCH_EXTRACTION_FOLDER_NAME = "KH1_PATCH";
        private const string KH1_OPENING_VIDEO_FILENAME = "OPN.mp4";
        private const string KH1_ENDING_VIDEO_FILENAME = "END.mp4";

        private const string DEFAULT_GAME_FOLDER = @"C:\Program Files\Epic Games\KH_1.5_2.5";
        private const string SAVE_FOLDER_NAME = "Patch/Saves";
        private const string PATCHED_FILES_FOLDER_NAME = "Patch/Temp";
        private static readonly List<string> REQUIRED_FILES = new List<string>()
        {
            "Image/en/kh1_first.pkg", "Image/en/kh1_first.hed",
            "Image/en/kh1_third.pkg", "Image/en/kh1_third.hed",
            "Image/en/kh1_fourth.pkg", "Image/en/kh1_fourth.hed",
            "Image/en/kh1_fifth.pkg", "Image/en/kh1_fifth.hed",
        };

        private Progress<List<object>> _progress;

        public event PropertyChangedEventHandler PropertyChanged;
        private float _patchState = 0f;
        private string _selectedGameFolder;

        public float PatchState
        {
            get { return _patchState; }
            set
            {
                _patchState = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("PatchState"));
                }
            }
        }

        public bool ShouldPatchMagic
        {
            get { return (bool)GetValue(ShouldPatchMagicProperty); }
            set { SetValue(ShouldPatchMagicProperty, value); }
        }

        public bool ShouldPatchTexture
        {
            get { return (bool)GetValue(ShouldPatchTextureProperty); }
            set { SetValue(ShouldPatchTextureProperty, value); }
        }

        public bool ShouldSaveOriginalFiles
        {
            get { return (bool)GetValue(ShouldSaveOriginalFilesProperty); }
            set { SetValue(ShouldSaveOriginalFilesProperty, value); }
        }

        public static readonly DependencyProperty ShouldPatchMagicProperty = DependencyProperty.Register("PatchMagicOption", typeof(bool), typeof(MainWindow), new UIPropertyMetadata(true));
        public static readonly DependencyProperty ShouldPatchTextureProperty = DependencyProperty.Register("PatchTextureOption", typeof(bool), typeof(MainWindow), new UIPropertyMetadata(true));
        public static readonly DependencyProperty ShouldSaveOriginalFilesProperty = DependencyProperty.Register("SaveOriginalFilesOption", typeof(bool), typeof(MainWindow), new UIPropertyMetadata(true));

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            SearchGameFolderState();

            _progress = new Progress<List<object>>(value =>
            {
                var copiedSize = (long)value[0];
                var totalFileSize = (long)value[1];
                var filename = (string)value[2];

                var progress = ((double)copiedSize / totalFileSize) * 100;

                PatchState = (float)progress;
                //Debug.WriteLine("{1} {0:N2}%", progress, filename);
            });

            if (CheckGameFolder(DEFAULT_GAME_FOLDER))
            {
                _selectedGameFolder = DEFAULT_GAME_FOLDER;
                ReadyToPatchState();
            }
            else
            {
                Debug.WriteLine("Default game folder not found.");
                //GameNotFoundWarningMessage.Visibility = Visibility.Collapsed;
                //GameNotFoundWarningMessage.Text =
                //    "Le dossier d'installation de Kingdom Hearts HD 1.5 + 2.5 ReMIX n'a pas été trouvé automatiquement. " +
                //    "Cliquez sur le bouton ci-dessous pour indiquer où vous avez installé le jeu sur votre machine.";
            }

            //FinishedState();
        }

        private void SearchGameFolderState()
        {
            GameNotFoundWarningMessage.Visibility = Visibility.Visible;
            BrowseButton.Visibility = Visibility.Visible;
            PatchButton.Visibility = Visibility.Collapsed;
            GameFoundMessage.Visibility = Visibility.Collapsed;
            PatchProgressionMessage.Visibility = Visibility.Collapsed;
            PatchProgressBar.Visibility = Visibility.Collapsed;
            Credits.Visibility = Visibility.Collapsed;
            PatchOptions.Visibility = Visibility.Collapsed;
        }

        private void ReadyToPatchState()
        {
            // TODO(bth): Show a button to unpatch the game

            if (CheckRemainingSpace(_selectedGameFolder))
            {
                GameNotFoundWarningMessage.Visibility = Visibility.Collapsed;
                BrowseButton.Visibility = Visibility.Collapsed;
                PatchButton.Visibility = Visibility.Visible;
                GameFoundMessage.Visibility = Visibility.Visible;
                Credits.Visibility = Visibility.Collapsed;
                PatchOptions.Visibility = Visibility.Visible;
                ImageHeight.Height = new GridLength(75);
            }
            else
            {
                var freeSpace = GetFreeSpace(_selectedGameFolder);
                GameNotFoundWarningMessage.Visibility = Visibility.Visible;
                GameNotFoundWarningMessage.Text = "Attention: ce patch s'assure de sauvegarder tous les fichiers originaux avant de les modifier afin que votre jeu ne soit pas cassé s'il y a un problème pendant le processus. " +
                    "Mais ces fichiers sont gros, 4 Go en tout et il semblerait que vous n'ayez pas suffisament d'espace pour pouvoir effectuer cette sauvegarde correctement." +
                    "Assurez-vous donc d'avoir suffisament d'espace libre avant de patcher votre jeu !";
            }
        }

        private void PatchingState()
        {
            GameNotFoundWarningMessage.Visibility = Visibility.Collapsed;
            GameFoundMessage.Visibility = Visibility.Collapsed;
            PatchProgressionMessage.Visibility = Visibility.Visible;
            PatchProgressBar.Visibility = Visibility.Visible;
            PatchButton.Visibility = Visibility.Collapsed;
            Credits.Visibility = Visibility.Collapsed;
            PatchOptions.Visibility = Visibility.Collapsed;
            ImageHeight.Height = new GridLength(250);
        }

        private void FinishedState()
        {
            GameNotFoundWarningMessage.Visibility = Visibility.Collapsed;
            PatchProgressionMessage.Text = $"Votre jeu a correctement été patché ! Profitez bien des voix françaises ;)";
            PatchProgressionMessage.FontWeight = FontWeights.Bold;
            PatchProgressionMessage.Visibility = Visibility.Visible;
            PatchProgressBar.Visibility = Visibility.Collapsed;
            Credits.Visibility = Visibility.Visible;
            ImageHeight.Height = new GridLength(0);
            BrowseButton.Visibility = Visibility.Collapsed;
            PatchOptions.Visibility = Visibility.Collapsed;
        }

        private void BrowsFolderButtonClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    Debug.WriteLine($"Selected: {dialog.SelectedPath}");

                    if (CheckGameFolder(dialog.SelectedPath))
                    {
                        _selectedGameFolder = dialog.SelectedPath;
                        ReadyToPatchState();
                    }
                    else
                    {
                        GameNotFoundWarningMessage.Visibility = Visibility.Visible;

                        GameNotFoundWarningMessage.Text =
                            "Le dossier d'installation de Kingdom Hearts HD 1.5 + 2.5 ReMIX que vous avez spécifié n'est pas valide.\n" +
                            "Il doit s'agir du dossier dans lequel l'Epic Game Store a téléchargé les fichiers du jeu.\n" +
                            $"Le dossier que vous avez donné: ";
                        GameNotFoundWarningMessage.Inlines.Add(new Italic(new Run($"\"{dialog.SelectedPath}\"")));

                        Debug.WriteLine("The selected folder is not valid!");
                    }
                }
            }
        }

        private void PatchGameButtonClick(object sender, RoutedEventArgs e)
        {
            PatchingState();
            _ = Patch(_selectedGameFolder);
        }

        private bool CheckGameFolder(string folder)
        {
            var directoryName = Path.GetFileName(folder);

            // Check PKG/HED files
            foreach (var requiredFile in REQUIRED_FILES)
            {
                var completePath = Path.Combine(folder, requiredFile);

                if (!File.Exists(completePath))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckRemainingSpace(string folder)
        {
            // Required at least 4GB to save original files
            return GetFreeSpace(folder) > 4e+9;
        }

        private long GetFreeSpace(string folder)
        {
            var folderInfo = new FileInfo(folder);
            DriveInfo drive = new DriveInfo(folderInfo.Directory.Root.FullName);

            // Required at least 4GB to save original files
            return drive.AvailableFreeSpace;
        }

        private async Task Patch(string gameFolder)
        {
            try
            {
                var patchesExtractionFolder = Path.Combine(PATCH_FOLDER, KH1_PATCH_EXTRACTION_FOLDER_NAME);
                //#if RELEASE
                // Make sure to remove patches extraction folders at first
                if (Directory.Exists(patchesExtractionFolder))
                    Directory.Delete(patchesExtractionFolder, true);
                //#endif

                // Extract VF patch files
                await ExtractPatch(KH1_PATCH_VOICES_ZIP_NAME);

                // Update videos if the corresponding patch is found
                await PatchVideos();

                // Extract "Magic" to "Magie" fix patch
                if (ShouldPatchMagic)
                {
                    await ExtractPatch(KH1_PATCH_MAGIC_ZIP_NAME);
                }

                if (ShouldPatchTexture)
                {
                    await ExtractPatch(KH1_PATCH_TEXTURES_ZIP_NAME);
                }

                // Create temporary folder to store patched files before to copy them in the actual game folder
                var patchedFilesBaseFolder = Path.Combine(gameFolder, PATCHED_FILES_FOLDER_NAME);

                if (Directory.Exists(patchedFilesBaseFolder))
                    Directory.Delete(patchedFilesBaseFolder, true);

                Directory.CreateDirectory(patchedFilesBaseFolder);

                foreach (var requiredFile in REQUIRED_FILES)
                {
                    var pkgFile = Path.Combine(gameFolder, requiredFile);
                    var patchFolder = Path.Combine(patchesExtractionFolder, Path.GetFileNameWithoutExtension(pkgFile));
                    var patchedPKGFile = Path.Combine(patchedFilesBaseFolder, Path.GetFileName(pkgFile));

                    if (Path.GetExtension(requiredFile) != ".pkg" || !Directory.Exists(patchFolder))
                        continue;

                    // Make sure to execute this for HED files too
                    await SaveOrRestore(gameFolder, Path.ChangeExtension(requiredFile, ".hed"));
                    await SaveOrRestore(gameFolder, requiredFile);

                    PatchProgressionMessage.Text = $"Modification de {Path.GetFileName(pkgFile)}...";

                    Patcher.PatchProgress += PatchProgress;
                    await Task.Run(() => Patcher.Patch(pkgFile, patchFolder, patchedFilesBaseFolder));

                    // Copy patched PKG to the right location
                    var progress = new Progress<List<object>>(value =>
                    {
                        var copiedSize = (long)value[0];
                        var totalFileSize = (long)value[1];
                        var filename = (string)value[2];

                        var progress = ((double)copiedSize / totalFileSize) * 100;

                        PatchState = (float)progress;
                        //Debug.WriteLine("{1} {0:N2}%", progress, filename);
                    });

                    var patchedHEDFile = Path.ChangeExtension(patchedPKGFile, ".hed");
                    var originalHEDFile = Path.ChangeExtension(pkgFile, ".hed");

                    PatchProgressionMessage.Text = $"Copie du fichier {Path.GetFileName(patchedHEDFile)} patché dans le dossier du jeu...";

                    await CopyToAsync(patchedHEDFile, originalHEDFile, progress, default);

                    PatchProgressionMessage.Text = $"Copie du fichier {Path.GetFileName(patchedPKGFile)} patché dans le dossier du jeu...";

                    await CopyToAsync(patchedPKGFile, pkgFile, progress, default, 0x1000000);
                }

                Directory.Delete(patchedFilesBaseFolder, true);

                FinishedState();
            }
            catch (Exception e)
            {
                PatchProgressionMessage.Foreground = Brushes.Red;
                PatchProgressionMessage.Text = $"Une erreur s'est produite: {e.Message}";

                Debug.WriteLine(e.Message);
            }
        }

        private async Task PatchVideos()
        {
            // If found, extract video patch
            if (File.Exists(Path.Combine(PATCH_FOLDER, KH1_PATCH_VIDEOS_ZIP_NAME)))
            {
                await ExtractPatch(KH1_PATCH_VIDEOS_ZIP_NAME);

                var openingVideoFile = Path.Combine(PATCH_FOLDER, KH1_PATCH_EXTRACTION_FOLDER_NAME, KH1_OPENING_VIDEO_FILENAME);
                var endingVideoFile = Path.Combine(PATCH_FOLDER, KH1_PATCH_EXTRACTION_FOLDER_NAME, KH1_ENDING_VIDEO_FILENAME);
                var gameVideosFolder = Path.Combine(_selectedGameFolder, @"EPIC\en\KH1Movie");
                var originalOpeningVideoFile = Path.Combine(gameVideosFolder, KH1_OPENING_VIDEO_FILENAME);
                var originalEndingVideoFile = Path.Combine(gameVideosFolder, KH1_ENDING_VIDEO_FILENAME);

                if (!Directory.Exists(gameVideosFolder))
                {
                    throw new Exception($"Le dossier {gameVideosFolder} n'existe pas, il est pourtant nécessaire pour patcher les vidéos du jeu.");
                }

                // Save original files
                await SaveOrRestore(_selectedGameFolder, originalOpeningVideoFile);
                await SaveOrRestore(_selectedGameFolder, originalEndingVideoFile);

                PatchProgressionMessage.Text = $"Copie du fichier {Path.GetFileName(openingVideoFile)} dans le dossier du jeu...";

                await CopyToAsync(openingVideoFile, originalOpeningVideoFile, _progress);

                PatchProgressionMessage.Text = $"Copie du fichier {Path.GetFileName(endingVideoFile)} dans le dossier du jeu...";

                await CopyToAsync(endingVideoFile, originalEndingVideoFile, _progress);
            }
        }

        private void PatchProgress(object sender, PatchProgressEventArgs e)
        {
            if (e.EntriesTotal > 0)
            {
                PatchState = Convert.ToInt32(100 * ((float)e.EntriesPatched / e.EntriesTotal));
            }
        }

        // fileToSaveOrRestore folder should be relative to game folder
        private async Task SaveOrRestore(string gameFolder, string fileToSaveOrRestore)
        {
            if (!ShouldSaveOriginalFiles)
            {
                return;
            }

            // Save the original files
            var saveFolder = Path.Combine(gameFolder, SAVE_FOLDER_NAME);
            var filename = Path.GetFileName(fileToSaveOrRestore);
            var savedFileCompletePath = Path.Combine(saveFolder, filename);
            var fileToSaveOrRestoreCompletePath = Path.Combine(gameFolder, fileToSaveOrRestore);

            string source;
            string destination;

            if (!File.Exists(savedFileCompletePath))
            {
                Directory.CreateDirectory(saveFolder);

                source = fileToSaveOrRestoreCompletePath;
                destination = savedFileCompletePath;

                PatchProgressionMessage.Text = $"Sauvegarde du fichier {filename} original...";
            }
            else
            {
                // Copy saved files in the original folder back, to make sure we patch the original files
                source = savedFileCompletePath;
                destination = fileToSaveOrRestoreCompletePath;

                PatchProgressionMessage.Text = $"Restauration de {filename}...";
            }

            await CopyToAsync(source, destination, _progress, default, 0x100000);
        }

        private async Task ExtractPatch(string patchName)
        {
            var patchFile = Path.Combine(PATCH_FOLDER, patchName);
            var extractionFolder = Path.Combine(PATCH_FOLDER, KH1_PATCH_EXTRACTION_FOLDER_NAME);

            if (!Directory.Exists(extractionFolder))
                Directory.CreateDirectory(extractionFolder);

            using (ZipFile zip = ZipFile.Read(patchFile))
            {
                // Make sure to extract patch files only if necessary
                var alreadyExtractedFiles = Directory.GetFiles(extractionFolder, "*.*", SearchOption.AllDirectories);
                var alreadyExtractedFolders = Directory.GetDirectories(extractionFolder, "*.*", SearchOption.AllDirectories);

                if (alreadyExtractedFiles.Length + alreadyExtractedFolders.Length != zip.Count)
                {
                    PatchProgressionMessage.Text = $"Extraction des fichiers de {patchName}...";
                    zip.ExtractProgress += new EventHandler<ExtractProgressEventArgs>(ZipExtractProgress);
                    await Task.Run(() => zip.ExtractAll(extractionFolder, ExtractExistingFileAction.OverwriteSilently));
                }
            }
        }

        void ZipExtractProgress(object sender, ExtractProgressEventArgs e)
        {
            if (e.EntriesTotal > 0)
            {
                PatchState = Convert.ToInt32(100 * ((float)e.EntriesExtracted / e.EntriesTotal));
            }
        }

        public async Task CopyToAsync(string sourceFile, string destinationFile, IProgress<List<object>> progress, CancellationToken cancellationToken = default(CancellationToken), int bufferSize = 0x1000)
        {
            var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var destinationStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[bufferSize];
            int bytesRead;
            long totalRead = 0;

            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await destinationStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                totalRead += bytesRead;
                progress.Report(new List<object>() { totalRead, sourceStream.Length, Path.GetFileName(sourceFile) });
            }

            sourceStream.Close();
            destinationStream.Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenURL(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void OnDonateClick(object sender, MouseButtonEventArgs e)
        {
            OpenURL(DONATE_URL);
            e.Handled = true;
        }

        private void OpenURL(string url)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://docs.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            var process = new ProcessStartInfo(url)
            {
                UseShellExecute = true
            };

            Process.Start(process);
        }
    }
}
