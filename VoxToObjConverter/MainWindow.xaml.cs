using System.Windows;
using VoxToObjConverter.Core.Services;

namespace VoxToObjConverter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var voxParser = new VoxParser();
            voxParser.ReadModel();
        }
    }
}