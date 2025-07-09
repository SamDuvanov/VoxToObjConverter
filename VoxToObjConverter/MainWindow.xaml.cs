using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VoxToObjConverter.Core;
using VoxToObjConverter.Core.Enums;

namespace VoxToObjConverter
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = files.All(f => f.ToLower().EndsWith(".vox"))
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = ((string[])e.Data.GetData(DataFormats.FileDrop))
                    .Where(f => f.ToLower().EndsWith(".vox"))
                    .ToList();

                AddFilesToList(files);
            }
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "VOX Files (*.vox)|*.vox",
                Multiselect = true,
            };

            if (dialog.ShowDialog() == true)
            {
                AddFilesToList(dialog.FileNames.ToList());
            }
        }

        private void AddFilesToList(List<string> files)
        {
            foreach (var file in files)
            {
                if (!FileListBox.Items.Contains(file))
                {
                    FileListBox.Items.Add(file);
                }
            }
        }

        private void ClearFileList_Click(object sender, RoutedEventArgs e)
        {
            FileListBox.Items.Clear();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void SelectOutputDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();

            if (dialog.ShowDialog() == true)
            {
                string folderPath = dialog.SelectedPath;
                OutputDirectoryTextBox.Text = folderPath;
            }
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
            {
                return;
            }

            string outputDir = OutputDirectoryTextBox.Text;
            string meshTypeText = ((MeshTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString()) ?? "Quad Mesh";
            MeshType meshType = meshTypeText == "Triangle Mesh" ? MeshType.Triangles : MeshType.Quads;
            List<string> voxFilePaths = FileListBox.Items.Cast<string>().ToList();

            ShowLoading(true);

            await RunCoverter(outputDir, meshType, voxFilePaths);

            ShowLoading(false);

            ShowInfoMessageBox("Conversion complete.");
        }

        private bool ValidateInputs()
        {
            if (FileListBox.Items.Count == 0)
            {
                ShowErrorMessageBox("Please add at least one .vox file.");

                return false;
            }

            foreach (var item in FileListBox.Items)
            {
                if (!File.Exists(item.ToString()))
                {
                    ShowErrorMessageBox($"File not found: {item}");

                    return false;
                }
            }

            string outputDir = OutputDirectoryTextBox.Text;

            if (string.IsNullOrWhiteSpace(outputDir) || !Directory.Exists(outputDir))
            {
                ShowErrorMessageBox("Please select a valid output directory.");

                return false;
            }

            return true;
        }

        private void ShowInfoMessageBox(string message)
        {
            MessageBox.Show(
            message,
            "Info",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        }

        private void ShowErrorMessageBox(string message)
        {
            MessageBox.Show(
            message,
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        }

        private void ShowLoading(bool show)
        {
            LoadingOverlay.Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Hidden;
            });
        }

        private static async Task RunCoverter(string outputDir, MeshType meshType, List<string> voxFilePaths)
        {
            await Task.Run(() =>
            {
                var options = new ConvertingOptions
                {
                    OutputDirectory = outputDir,
                    VoxFilePaths = voxFilePaths,
                    MeshType = meshType,
                };

                var converter = new ConvertingManager(options);
                converter.ConvertVoxToObj();
            });
        }
    }
}