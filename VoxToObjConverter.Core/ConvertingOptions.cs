using VoxToObjConverter.Core.Enums;

namespace VoxToObjConverter.Core;

/// <summary>
/// Represents the options for converting Vox files to OBJ format.
/// </summary>
public class ConvertingOptions
{
    /// <summary>
    /// Gets or sets the list of Vox file paths to convert.
    /// </summary>
    public required List<string> VoxFilePaths { get; set; }

    /// <summary>
    /// Gets or sets the output directory where the converted OBJ files will be saved.
    /// </summary>
    public required string OutputDirectory { get; set; }

    /// <summary>
    /// Gets or sets the type of mesh to generate from the Vox files.
    /// </summary>
    public MeshType MeshType { get; set; } = MeshType.Quads;
}
