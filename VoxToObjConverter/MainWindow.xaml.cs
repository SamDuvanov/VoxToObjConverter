using g3;
using gs;
using System.Windows;
using VoxReader;
using VoxToObjConverter.Core.Services.MeshServices;
using VoxToObjConverter.Core.Services.VoxelServices;

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
            var voxelModel = voxParser.ReadModel();
            var voxOptimizer = new VoxelModelOptimizer();
            IEnumerable<Voxel> optimizedVoxelModel = voxOptimizer.OptimizeModel(voxelModel);

            var meshBuilder = new MeshBuilder();
            var mesh = meshBuilder.GenerateSolidBoxyMesh(optimizedVoxelModel);

            var welder = new MeshWelder(mesh);
            welder.Weld();

            var meshToObjExporter = new QuadObjExporter();
            var meshToObjExporter2 = new MeshToObjExporter();
            meshToObjExporter2.ExportToFile(mesh, "model_optimized.obj");
        }
    }
}