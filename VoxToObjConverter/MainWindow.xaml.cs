using g3;
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

            var meshPrimitiveFactory = new SolidMeshBuilder();
            var mesh = meshPrimitiveFactory.CreateMeshFromVoxels(optimizedVoxelModel);

            var meshToObjExporter = new MeshToObjExporter();
            meshToObjExporter.ExportToFile(mesh, "model_optimized.obj");
        }
    }
}