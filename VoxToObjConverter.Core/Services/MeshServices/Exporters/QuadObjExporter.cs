using g3;
using System.Text;

namespace VoxToObjConverter.Core.Services.MeshServices.Exporters;

/// <summary>
/// Exports a DMesh3 mesh to an .obj file using quads where possible.
/// Only quads with near-rectangular shape (≈90° angles) are created.
/// </summary>
public class QuadObjExporter
{
    private const double CosAngleTolerance = 0.15; // Tolerance for cosine of 90° (approx 81.3 to 98.7 degrees)

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
            // Use standard formatting to avoid locale issues (e.g., commas in decimals)
            sb.AppendLine(string.Format("v {0:F6} {1:F6} {2:F6}", v.x, v.y, v.z));
        }

        foreach (int vid in mesh.VertexIndices())
        {
            var n = mesh.GetVertexNormal(vid);
            sb.AppendLine(string.Format("vn {0:F6} {1:F6} {2:F6}", n.x, n.y, n.z));
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

        foreach (int tid1 in mesh.TriangleIndices())
        {
            if (processedTriangles.Contains(tid1))
            {
                continue;
            }

            Index3i tri1_verts = mesh.GetTriangle(tid1);

            // Iterate over the edges of triangle 1
            for (int i = 0; i < 3; i++)
            {
                int v1 = tri1_verts[i];
                int v2 = tri1_verts[(i + 1) % 3]; // The next vertex in the triangle's winding order

                int commonEdgeID = mesh.FindEdge(v1, v2);

                if (commonEdgeID == DMesh3.InvalidID) continue; // Should not happen for valid triangles

                // Get the two triangles connected by this edge
                Index2i edgeTris = mesh.GetEdgeT(commonEdgeID);

                // Find the neighbor triangle (the one that is not 'tid1')
                int tid2 = DMesh3.InvalidID;
                if (edgeTris.a == tid1)
                {
                    tid2 = edgeTris.b;
                }
                else if (edgeTris.b == tid1)
                {
                    tid2 = edgeTris.a;
                }

                // If a valid, unprocessed neighbor is found, attempt to form a quad
                if (tid2 != DMesh3.InvalidID && !processedTriangles.Contains(tid2))
                {
                    AttemptAddQuad(mesh, tid1, tid2, quads, processedTriangles);
                    // If a quad was successfully formed using 'tid1', 'tid1' is now processed.
                    // We can break and move to the next unprocessed triangle.
                    if (processedTriangles.Contains(tid1))
                        break;
                }
            }
        }
        return quads;
    }

    private void AttemptAddQuad(DMesh3 mesh, int tid1, int tid2, List<QuadInfo> quads, HashSet<int> processedTriangles)
    {
        QuadInfo? quad = CreateQuad(mesh, tid1, tid2);
        if (quad != null)
        {
            quads.Add(quad);
            processedTriangles.Add(tid1);
            processedTriangles.Add(tid2);
        }
    }


    /// <summary>
    /// Attempts to create a valid rectangular quad from two triangles.
    /// </summary>
    private QuadInfo? CreateQuad(DMesh3 mesh, int t1Id, int t2Id)
    {
        Index3i tri1 = mesh.GetTriangle(t1Id);
        Index3i tri2 = mesh.GetTriangle(t2Id);

        // Find the common vertices (the shared edge) between tri1 and tri2
        var commonVertices = tri1.array.Intersect(tri2.array).ToList();

        if (commonVertices.Count != 2)
        {
            // Triangles must share exactly two vertices (an edge) to form a quad
            return null;
        }

        // Get the non-common (opposite) vertices
        // These are the "tips" of the two triangles that form the diagonal of the quad
        var uniqueV1 = tri1.array.Except(commonVertices).Single();
        var uniqueV2 = tri2.array.Except(commonVertices).Single();

        // The four vertices of the potential quad are: 
        // commonVertices[0], uniqueV1, commonVertices[1], uniqueV2.
        // We put them in this initial order, and the OrderQuadVertices will sort them correctly.
        List<int> quadVertices = new List<int>
        {
            commonVertices[0],
            uniqueV1,
            commonVertices[1],
            uniqueV2
        };


        // Get the normal of the first triangle for consistent ordering
        Vector3d referenceNormal = mesh.GetTriNormal(t1Id);

        int[] ordered = OrderQuadVertices(mesh, quadVertices, referenceNormal);

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
    /// Orders 4 vertices in a consistent counter-clockwise order based on a reference normal.
    /// </summary>
    private int[] OrderQuadVertices(DMesh3 mesh, List<int> vertices, Vector3d normal)
    {
        if (vertices.Count != 4)
            return vertices.ToArray();

        Vector3d center = Vector3d.Zero;
        foreach (int id in vertices)
        {
            center += mesh.GetVertex(id);
        }
        center /= 4.0;

        // Create an orthonormal basis for the plane perpendicular to the normal.
        // This is crucial for consistent 2D projection for angle sorting.
        // Ensure that u and v are truly orthogonal and non-zero.
        Vector3d u, v;
        if (normal.Cross(Vector3d.AxisY).LengthSquared > MathUtil.Epsilon) // If normal is not parallel to Y-axis
            u = normal.Cross(Vector3d.AxisY).Normalized;
        else if (normal.Cross(Vector3d.AxisX).LengthSquared > MathUtil.Epsilon) // If normal is not parallel to X-axis
            u = normal.Cross(Vector3d.AxisX).Normalized;
        else // Fallback if normal is parallel to both X and Y (i.e., along Z)
            u = normal.Cross(Vector3d.AxisZ).Normalized;

        v = normal.Cross(u).Normalized; // v will be orthogonal to both normal and u

        // Sort vertices by angle around the center in the plane defined by u and v.
        // This ensures counter-clockwise ordering when viewed from the direction of the normal.
        return vertices
            .OrderBy(id =>
            {
                Vector3d point = mesh.GetVertex(id);
                Vector3d dir = point - center;

                // Project 'dir' onto the plane defined by 'u' and 'v'
                double x2d = dir.Dot(u);
                double y2d = dir.Dot(v);

                return Math.Atan2(y2d, x2d);
            })
            .Select(id => id)
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
            Vector3d p_prev = points[(i + 3) % 4]; // Previous vertex in cyclic order
            Vector3d p_curr = points[i];           // Current vertex (at the corner)
            Vector3d p_next = points[(i + 1) % 4]; // Next vertex in cyclic order

            // Check for degenerate edges (points too close)
            if ((p_curr - p_prev).LengthSquared < MathUtil.Epsilon ||
                (p_curr - p_next).LengthSquared < MathUtil.Epsilon)
            {
                return false;
            }

            Vector3d edge1 = (p_prev - p_curr).Normalized;
            Vector3d edge2 = (p_next - p_curr).Normalized;

            // The dot product of two normalized vectors is the cosine of the angle between them.
            // For 90 degrees, the cosine is 0. We check if it's close to 0 within a tolerance.
            if (Math.Abs(edge1.Dot(edge2)) > CosAngleTolerance)
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