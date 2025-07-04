using g3;
using System.Text;

/// <summary>
/// Exports a DMesh3 mesh to an .obj file using quads where possible.
/// Only quads with near-rectangular shape (≈90° angles) are created.
/// </summary>
public class QuadObjExporter
{
    private const double CosAngleTolerance = 0.15; // Tolerance for cosine of 90°

    /// <summary>
    /// Exports the given mesh to an .obj file with quads and triangles.
    /// </summary>
    /// <param name="mesh">The DMesh3 mesh to export.</param>
    /// <param name="filePath">Destination file path for the .obj output.</param>
    public void ExportToFile(DMesh3 mesh, string filePath)
    {
        var sb = new StringBuilder();

        // Write vertex positions (1-based indexing in OBJ format)
        foreach (int vid in mesh.VertexIndices())
        {
            Vector3d v = mesh.GetVertex(vid);
            sb.AppendLine($"v {v.x:F6} {v.y:F6} {v.z:F6}");
        }

        // Extract quad candidates from adjacent triangles
        var quads = ExtractQuadsFromMesh(mesh);
        var processedTriangles = new HashSet<int>();

        // Write quads
        foreach (QuadInfo quad in quads)
        {
            int[] indices = quad.Vertices.Select(idx => idx + 1).ToArray();
            sb.AppendLine($"f {indices[0]} {indices[1]} {indices[2]} {indices[3]}");

            processedTriangles.Add(quad.Triangle1);
            processedTriangles.Add(quad.Triangle2);
        }

        // Write leftover triangles
        foreach (int tid in mesh.TriangleIndices())
        {
            if (processedTriangles.Contains(tid))
            {
                continue;
            }

            Index3i tri = mesh.GetTriangle(tid);
            sb.AppendLine($"f {tri.a + 1} {tri.b + 1} {tri.c + 1}");
        }

        File.WriteAllText(filePath, sb.ToString());
    }

    /// <summary>
    /// Tries to find quad pairs from adjacent triangles in the mesh.
    /// </summary>
    private List<QuadInfo> ExtractQuadsFromMesh(DMesh3 mesh)
    {
        var quads = new List<QuadInfo>();
        var processedTriangles = new HashSet<int>();

        foreach (int tid in mesh.TriangleIndices())
        {
            if (processedTriangles.Contains(tid))
            {
                continue;
            }

            Vector3d normal = mesh.GetTriNormal(tid);
            Vector3d centroid = mesh.GetTriCentroid(tid);

            int? pairTid = FindQuadPair(mesh, tid, normal, centroid, processedTriangles);

            if (pairTid.HasValue)
            {
                QuadInfo quad = CreateQuad(mesh, tid, pairTid.Value);
                if (quad != null)
                {
                    quads.Add(quad);
                    processedTriangles.Add(tid);
                    processedTriangles.Add(pairTid.Value);
                }
            }
        }

        return quads;
    }

    /// <summary>
    /// Finds a triangle that can form a quad with the given triangle.
    /// </summary>
    private int? FindQuadPair(
        DMesh3 mesh,
        int triangleId,
        Vector3d normal,
        Vector3d centroid,
        HashSet<int> processed)
    {
        Index3i tri = mesh.GetTriangle(triangleId);
        int[] vertices = { tri.a, tri.b, tri.c };

        foreach (int vid in vertices)
        {
            foreach (int neighborTid in mesh.VtxTrianglesItr(vid))
            {
                if (neighborTid == triangleId || processed.Contains(neighborTid))
                {
                    continue;
                }

                Vector3d neighborNormal = mesh.GetTriNormal(neighborTid);

                if (Vector3d.Dot(normal, neighborNormal) < 0.99)
                {
                    continue;
                }

                Vector3d neighborCentroid = mesh.GetTriCentroid(neighborTid);
                double planeDiff = Vector3d.Dot(normal, centroid - neighborCentroid);

                if (Math.Abs(planeDiff) > 1e-6)
                {
                    continue;
                }

                Index3i neighborTri = mesh.GetTriangle(neighborTid);

                if (CountSharedVertices(tri, neighborTri) == 2)
                {
                    return neighborTid;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Counts how many vertices are shared between two triangles.
    /// </summary>
    private int CountSharedVertices(Index3i t1, Index3i t2)
    {
        int[] v1 = { t1.a, t1.b, t1.c };
        int[] v2 = { t2.a, t2.b, t2.c };

        return v1.Count(v => v2.Contains(v));
    }

    /// <summary>
    /// Attempts to create a valid rectangular quad from two triangles.
    /// </summary>
    private QuadInfo? CreateQuad(DMesh3 mesh, int t1Id, int t2Id)
    {
        Index3i tri1 = mesh.GetTriangle(t1Id);
        Index3i tri2 = mesh.GetTriangle(t2Id);

        var allVertices = new[] { tri1.a, tri1.b, tri1.c, tri2.a, tri2.b, tri2.c };
        var uniqueVertices = allVertices.Distinct().ToList();

        if (uniqueVertices.Count != 4)
        {
            return null;
        }

        int[] ordered = OrderQuadVertices(mesh, uniqueVertices);

        if (!IsRectangular(mesh, ordered))
        {
            return null;
        }

        return new QuadInfo
        {
            Vertices = ordered,
            Triangle1 = t1Id,
            Triangle2 = t2Id
        };
    }

    /// <summary>
    /// Orders 4 vertices in a consistent circular order based on center and normal.
    /// </summary>
    private int[] OrderQuadVertices(DMesh3 mesh, List<int> vertices)
    {
        if (vertices.Count != 4)
            return vertices.ToArray();

        Vector3d center = vertices
            .Select(id => mesh.GetVertex(id))
            .Aggregate(Vector3d.Zero, (sum, v) => sum + v) / 4.0;

        // Estimate face normal from first 3 points
        Vector3d v0 = mesh.GetVertex(vertices[0]);
        Vector3d v1 = mesh.GetVertex(vertices[1]);
        Vector3d v2 = mesh.GetVertex(vertices[2]);
        Vector3d normal = Vector3d.Cross(v1 - v0, v2 - v0).Normalized;

        // Create basis in the plane of the quad
        Vector3d refDir = Vector3d.Cross(normal, new Vector3d(0, 0, 1));
        if (refDir.LengthSquared < 1e-6)
            refDir = Vector3d.Cross(normal, new Vector3d(0, 1, 0));
        refDir.Normalize();

        Vector3d upDir = Vector3d.Cross(refDir, normal).Normalized;

        return vertices
            .Select(id => new
            {
                Id = id,
                Vertex = mesh.GetVertex(id)
            })
            .OrderBy(v =>
            {
                Vector3d dir = v.Vertex - center;
                return Math.Atan2(Vector3d.Dot(dir, upDir), Vector3d.Dot(dir, refDir));
            })
            .Select(v => v.Id)
            .ToArray();
    }

    /// <summary>
    /// Validates if the 4 points form a nearly-rectangular quad.
    /// </summary>
    private bool IsRectangular(DMesh3 mesh, int[] vertices)
    {
        if (vertices.Length != 4)
        {
            return false;
        }

        Vector3d[] points = vertices.Select(v => mesh.GetVertex(v)).ToArray();

        for (int i = 0; i < 4; i++)
        {
            Vector3d a = points[i] - points[(i + 3) % 4];
            Vector3d b = points[(i + 1) % 4] - points[i];
            double dot = a.Normalized.Dot(b.Normalized);

            if (Math.Abs(dot) > CosAngleTolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Represents a valid quad composed of two adjacent triangles.
    /// </summary>
    private class QuadInfo
    {
        public int[] Vertices { get; set; } = Array.Empty<int>();
        public int Triangle1 { get; set; }
        public int Triangle2 { get; set; }
    }
}
