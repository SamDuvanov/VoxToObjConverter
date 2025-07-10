using VoxReader;
using VoxToObjConverter.Core.Services.MeshServices.Exporters;
using VoxToObjConverter.Core.Services.MeshServices;
using VoxToObjConverter.Core.Services.VoxelServices;
using VoxToObjConverter.Core.Enums;
using VoxReader.Interfaces;
using g3;
using VoxToObjConverter.Core.Services.MeshServices.Utils;

namespace VoxToObjConverter.Core;

/// <summary>
/// Performs full conversion of .vox files to .obj format.
/// </summary>
public class VoxModelConverter
{
    private readonly ConvertingOptions _convertingOptions;

    public VoxModelConverter(ConvertingOptions convertingOptions)
    {
        _convertingOptions = convertingOptions ??
            throw new ArgumentNullException(nameof(convertingOptions));
    }

    /// <summary>
    /// Converts all specified .vox files to .obj format basing on the provided options.
    /// </summary>
    public void ConvertVoxToObj()
    {
        foreach (string filePath in _convertingOptions.VoxFilePaths)
        {
            IModel[] voxelModels = ParseVoxFile(filePath);
            DMesh3 resultMesh = GetCombinedSolidTriangleMesh(voxelModels);
            ExportMeshToObjFile(filePath, resultMesh);
        }
    }

    private static IModel[] ParseVoxFile(string filePath)
    {
        var voxParser = new VoxModelParser();
        var voxelModels = voxParser.ReadModel(filePath);

        return voxelModels;
    }

    /// <summary>
    /// Combines all voxel models into a single solid triangle mesh.
    /// </summary>
    /// <param name="voxelModels">Voxel models from the single vox file.</param>
    /// <returns></returns>
    private DMesh3 GetCombinedSolidTriangleMesh(IModel[] voxelModels)
    {
        DMesh3 combinedMesh = new DMesh3();
        var transformMeshUtility = new TransformMeshUtility();

        foreach (IModel voxelModel in voxelModels)
        {
            IEnumerable<Voxel> optimizedVoxels = OptimizeVoxelModel(voxelModel);
            DMesh3 originalMesh = CreateTrianglesMesh(optimizedVoxels);
            WeldTrianglesIntoSolidMesh(originalMesh);
            DMesh3 transformedMesh = new DMesh3(originalMesh);
            transformMeshUtility.ApplyTransformToMesh(voxelModel, transformedMesh);
            AddMeshToParent(combinedMesh, transformedMesh);
        }

        return combinedMesh;

        static void AddMeshToParent(DMesh3 parentMesh, DMesh3 meshToAdd)
        {
            MeshEditor editor = new MeshEditor(parentMesh);
            editor.AppendMesh(meshToAdd);
        }
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