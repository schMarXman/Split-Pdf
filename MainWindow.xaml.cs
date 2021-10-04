using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Split
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = MainWindowViewModel.Instance;
            InitializeComponent();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var file = ((string[])e.Data.GetData(DataFormats.FileDrop)).First();
                if (DataContext is MainWindowViewModel dc)
                {
                    dc.OpenPdf(file);
                }
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            bool fileAllowed = false;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                FileInfo file = null;

                try
                {
                    file = new FileInfo(((string[])e.Data.GetData(DataFormats.FileDrop)).First());
                }
                catch (Exception)
                {
                    // file inaccessible
                }

                fileAllowed = file != null && file.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
            }

            if (!fileAllowed)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void PreviewTextNumericOnly(object sender, TextCompositionEventArgs e)
        {
            Regex numericRegex = new Regex(@"\d");
            e.Handled = !numericRegex.IsMatch(e.Text);
        }

        private void PreviewTextNoIllegalPathChars(object sender, TextCompositionEventArgs e)
        {
            foreach (var character in System.IO.Path.GetInvalidFileNameChars())
            {
                if (e.Text.Contains(character))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel dc)
            {
                dc.OnWindowLoaded?.Invoke();
            }
        }
    }
}
