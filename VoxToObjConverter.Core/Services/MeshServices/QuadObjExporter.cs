using g3;
using System.Text;

public class QuadObjExporter
{
    public void ExportToFile(DMesh3 mesh, string filePath)
    {
        var sb = new StringBuilder();

        // Write vertices
        foreach (int vid in mesh.VertexIndices())
        {
            var vertex = mesh.GetVertex(vid);
            sb.AppendLine($"v {vertex.x:F6} {vertex.y:F6} {vertex.z:F6}");
        }

        // Group triangles into quads
        var quads = ExtractQuadsFromMesh(mesh);
        var processedTriangles = new HashSet<int>();

        // Write quads
        foreach (var quad in quads)
        {
            // OBJ indices start from 1
            var indices = quad.Vertices.Select(idx => idx + 1).ToArray();
            sb.AppendLine($"f {indices[0]} {indices[1]} {indices[2]} {indices[3]}");

            // Mark triangles as processed
            processedTriangles.Add(quad.Triangle1);
            processedTriangles.Add(quad.Triangle2);
        }

        // Write remaining triangles
        foreach (int tid in mesh.TriangleIndices())
        {
            if (!processedTriangles.Contains(tid))
            {
                var triangle = mesh.GetTriangle(tid);
                sb.AppendLine($"f {triangle.a + 1} {triangle.b + 1} {triangle.c + 1}");
            }
        }

        File.WriteAllText(filePath, sb.ToString());
    }

    private List<QuadInfo> ExtractQuadsFromMesh(DMesh3 mesh)
    {
        var quads = new List<QuadInfo>();
        var processedTriangles = new HashSet<int>();

        foreach (int tid in mesh.TriangleIndices())
        {
            if (processedTriangles.Contains(tid))
                continue;

            var triangle = mesh.GetTriangle(tid);
            var normal = mesh.GetTriNormal(tid);
            var centroid = mesh.GetTriCentroid(tid);

            // Search for a neighboring triangle to form a quad
            int? pairTriangle = FindQuadPair(mesh, tid, normal, centroid, processedTriangles);

            if (pairTriangle.HasValue)
            {
                var quad = CreateQuad(mesh, tid, pairTriangle.Value);
                if (quad != null)
                {
                    quads.Add(quad);
                    processedTriangles.Add(tid);
                    processedTriangles.Add(pairTriangle.Value);
                }
            }
        }

        return quads;
    }

    private int? FindQuadPair(DMesh3 mesh, int triangleId, Vector3d normal, Vector3d centroid, HashSet<int> processedTriangles)
    {
        var triangle = mesh.GetTriangle(triangleId);
        var vertices = new[] { triangle.a, triangle.b, triangle.c };

        // Check neighboring triangles for each vertex
        foreach (int vid in vertices)
        {
            foreach (int neighborTid in mesh.VtxTrianglesItr(vid))
            {
                if (neighborTid == triangleId || processedTriangles.Contains(neighborTid))
                    continue;

                var neighborTriangle = mesh.GetTriangle(neighborTid);
                var neighborNormal = mesh.GetTriNormal(neighborTid);
                var neighborCentroid = mesh.GetTriCentroid(neighborTid);

                // Check if the triangles have the same normal
                if (Vector3d.Dot(normal, neighborNormal) < 0.99)
                    continue;

                // Check if the triangles lie in the same plane
                var planeDiff = Vector3d.Dot(normal, centroid - neighborCentroid);
                if (Math.Abs(planeDiff) > 1e-6)
                    continue;

                // Check if the triangles share an edge
                if (CountSharedVertices(triangle, neighborTriangle) == 2)
                {
                    return neighborTid;
                }
            }
        }

        return null;
    }

    private int CountSharedVertices(Index3i tri1, Index3i tri2)
    {
        var vertices1 = new[] { tri1.a, tri1.b, tri1.c };
        var vertices2 = new[] { tri2.a, tri2.b, tri2.c };

        return vertices1.Count(v => vertices2.Contains(v));
    }

    private QuadInfo CreateQuad(DMesh3 mesh, int tri1Id, int tri2Id)
    {
        var tri1 = mesh.GetTriangle(tri1Id);
        var tri2 = mesh.GetTriangle(tri2Id);

        // Find all unique vertices
        var allVertices = new List<int> { tri1.a, tri1.b, tri1.c, tri2.a, tri2.b, tri2.c };
        var uniqueVertices = allVertices.Distinct().ToList();

        if (uniqueVertices.Count != 4)
            return null;

        // Order the vertices of the quad
        var orderedVertices = OrderQuadVertices(mesh, uniqueVertices);

        return new QuadInfo
        {
            Vertices = orderedVertices,
            Triangle1 = tri1Id,
            Triangle2 = tri2Id
        };
    }

    private int[] OrderQuadVertices(DMesh3 mesh, List<int> vertices)
    {
        if (vertices.Count != 4)
            return vertices.ToArray();

        // Find the center of the quad
        var center = Vector3d.Zero;
        foreach (int vid in vertices)
        {
            center += mesh.GetVertex(vid);
        }
        center /= 4.0;

        // Find the normal of the quad
        var v0 = mesh.GetVertex(vertices[0]);
        var v1 = mesh.GetVertex(vertices[1]);
        var v2 = mesh.GetVertex(vertices[2]);
        var normal = Vector3d.Cross(v1 - v0, v2 - v0).Normalized;

        // Order vertices by angle around the center
        var sortedVertices = vertices
            .Select(vid => new
            {
                Id = vid,
                Vertex = mesh.GetVertex(vid)
            })
            .OrderBy(v =>
            {
                var dir = v.Vertex - center;

                // Create basis vectors for the quad's plane
                var right = Vector3d.Cross(normal, new Vector3d(0, 0, 1)).Normalized;
                if (right.LengthSquared < 0.1)
                    right = Vector3d.Cross(normal, new Vector3d(0, 1, 0)).Normalized;
                var up = Vector3d.Cross(right, normal).Normalized;

                return Math.Atan2(Vector3d.Dot(dir, up), Vector3d.Dot(dir, right));
            })
            .Select(v => v.Id)
            .ToArray();

        return sortedVertices;
    }

    private class QuadInfo
    {
        public int[] Vertices { get; set; }
        public int Triangle1 { get; set; }
        public int Triangle2 { get; set; }
    }
}