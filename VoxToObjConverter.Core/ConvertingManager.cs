using VoxReader;
using VoxToObjConverter.Core.Services.MeshServices.Exporters;
using VoxToObjConverter.Core.Services.MeshServices;
using VoxToObjConverter.Core.Services.VoxelServices;
using VoxToObjConverter.Core.Enums;
using VoxReader.Interfaces;
using g3;

namespace VoxToObjConverter.Core;

/// <summary>
/// Performs full conversion of .vox files to .obj format.
/// </summary>
public class ConvertingManager
{
    private readonly ConvertingOptions _convertingOptions;

    public ConvertingManager(ConvertingOptions convertingOptions)
    {
        _convertingOptions = convertingOptions ??
            throw new ArgumentNullException(nameof(convertingOptions));
    }

    public void ConvertVoxToObj()
    {
        foreach (string filePath in _convertingOptions.VoxFilePaths)
        {
            IModel voxelModel = ParseVoxFile(filePath);
            IEnumerable<Voxel> optimizedVoxelModel = OptimizeVoxelModel(voxelModel);
            DMesh3 mesh = CreateTrianglesMesh(optimizedVoxelModel);
            WeldTrianglesIntoSolidMesh(mesh);
            ExportMeshToObjFile(filePath, mesh);
        }
    }

    private static IModel ParseVoxFile(string filePath)
    {
        var voxParser = new VoxModelParser();
        var voxelModel = voxParser.ReadModel(filePath);

        return voxelModel;
    }

    private static IEnumerable<Voxel> OptimizeVoxelModel(IModel voxelModel)
    {
        var voxOptimizer = new VoxelModelOptimizer();
        IEnumerable<Voxel> optimizedVoxelModel = voxOptimizer.OptimizeModel(voxelModel);

        return optimizedVoxelModel;
    }

    private static DMesh3 CreateTrianglesMesh(IEnumerable<Voxel> optimizedVoxelModel)
    {
        var meshBuilder = new MeshBuilder();
        var mesh = meshBuilder.GenerateSolidBoxyMesh(optimizedVoxelModel);

        return mesh;
    }

    private static void WeldTrianglesIntoSolidMesh(DMesh3 mesh)
    {
        var welder = new MeshWelder(mesh);
        welder.Weld();
    }

    private void ExportMeshToObjFile(string filePath, DMesh3 mesh)
    {
        IMeshToObjExporter exporter = _convertingOptions.MeshType switch
        {
            MeshType.Quads => new QuadMeshToObjExporter(),
            _ => new TriangleMeshToObjExporter()
        };

        string outputPath = GetOutputPath(filePath);
        exporter.ExportToFile(mesh, outputPath);
    }

    private string GetOutputPath(string filePath)
    {
        string targetDirectory = _convertingOptions.OutputDirectory;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

        return Path.Combine(targetDirectory, $"{fileNameWithoutExtension}_converted.obj");
    }
}