using g3;
using System.Text;
using VoxReader;

namespace VoxToObjConverter.Core.Services.MeshServices
{
    public class MeshToObjExporter
    {
        /// <summary>
        /// Exports a DMesh3 mesh to an OBJ format string.
        /// </summary>
        public string ExportToString(DMesh3 mesh)
        {
            var sb = new StringBuilder();

            // Write vertices
            foreach (int vid in mesh.VertexIndices())
            {
                var v = mesh.GetVertex(vid);
                sb.AppendLine($"v {v.x} {v.y} {v.z}");
            }

            // Write faces (OBJ indices start at 1)
            foreach (int tid in mesh.TriangleIndices())
            {
                var tri = mesh.GetTriangle(tid);
                sb.AppendLine($"f {tri.a + 1} {tri.b + 1} {tri.c + 1}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports a DMesh3 mesh to an OBJ file at given path.
        /// </summary>
        public void ExportToFile(DMesh3 mesh, string filePath)
        {
            var objContent = ExportToString(mesh);
            File.WriteAllText(filePath, objContent);
        }
    }
}
