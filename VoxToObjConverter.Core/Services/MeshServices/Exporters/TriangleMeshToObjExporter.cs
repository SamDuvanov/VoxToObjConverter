using g3;
using System.Text;

namespace VoxToObjConverter.Core.Services.MeshServices.Exporters;

/// <summary>
/// Exports <see cref="DMesh3"/> as triangle mesh data in OBJ file format.
/// </summary>
public class TriangleMeshToObjExporter : IMeshToObjExporter
{
    /// <summary>
    /// Saves the mesh in OBJ format to the specified file path.
    /// </summary>
    public void ExportToFile(DMesh3 mesh, string filePath)
    {
        //FixTriangleWinding(mesh);
        //RecalculateNormals(mesh);

        var objContent = ConvertMeshToObjString(mesh);
        File.WriteAllText(filePath, objContent);
    }

    /// <summary>
    /// Converts the mesh into a string in OBJ format.
    /// </summary>
    private string ConvertMeshToObjString(DMesh3 mesh)
    {
        var builder = new StringBuilder();

        AppendVertices(mesh, builder);
        AppendNormals(mesh, builder);
        AppendFaces(mesh, builder);

        return builder.ToString();
    }

    /// <summary>
    /// Appends vertex positions to the OBJ content string.
    /// </summary>
    private static void AppendVertices(DMesh3 mesh, StringBuilder builder)
    {
        foreach (int vertexId in mesh.VertexIndices())
        {
            var vertex = mesh.GetVertex(vertexId);
            builder.AppendLine(string.Format("v {0:F6} {1:F6} {2:F6}", vertex.x, vertex.y, vertex.z));
        }
    }

    /// <summary>
    /// Appends triangle face definitions to the OBJ content string.
    /// </summary>
    private static void AppendFaces(DMesh3 mesh, StringBuilder builder)
    {
        foreach (int triangleId in mesh.TriangleIndices())
        {
            /// OBJ indices are 1-based, so we increment each index by 1.
            var triangle = mesh.GetTriangle(triangleId);
            int a = triangle.a + 1;
            int b = triangle.b + 1;
            int c = triangle.c + 1;
            builder.AppendLine($"f {a} {b} {c}");
        }
    }

    /// <summary>
    /// Appends vertex normals to the OBJ content string.
    /// </summary>
    private static void AppendNormals(DMesh3 mesh, StringBuilder builder)
    {
        foreach (int vertexId in mesh.VertexIndices())
        {
            var normal = mesh.GetVertexNormal(vertexId);
            builder.AppendLine(string.Format("vn {0:F6} {1:F6} {2:F6}", normal.x, normal.y, normal.z));
        }
    }

    /// <summary>
    /// Fixes triangle winding so normals face outward.
    /// </summary>
    private static void FixTriangleWinding(DMesh3 mesh)
    {
        for (int tid = 0; tid < mesh.TriangleCount; tid++)
        {
            if (!mesh.IsTriangle(tid)) continue;

            Index3i tri = mesh.GetTriangle(tid);

            Vector3d a = mesh.GetVertex(tri.a);
            Vector3d b = mesh.GetVertex(tri.b);
            Vector3d c = mesh.GetVertex(tri.c);

            Vector3d ab = b - a;
            Vector3d ac = c - a;

            Vector3d geometricNormal = ab.Cross(ac).Normalized;
            Vector3d currentNormal = mesh.GetTriNormal(tid);

            if (geometricNormal.Dot(currentNormal) < 0)
            {
                // Flip triangle orientation to correct normal
                mesh.SetTriangle(tid, new Index3i(tri.a, tri.c, tri.b));
            }
        }
    }

    private static void RecalculateNormals(DMesh3 mesh)
    {
        var normals = new MeshNormals(mesh);
        normals.Compute();
        normals.CopyTo(mesh); // sets vertex normals into mesh
    }
}
