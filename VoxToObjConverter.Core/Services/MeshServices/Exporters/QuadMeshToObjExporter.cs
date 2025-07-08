using g3;
using System.Text;

namespace VoxToObjConverter.Core.Services.MeshServices.Exporters;

/// <summary>
/// Exports DMesh3 geometry as quad-based mesh data in OBJ file format.
/// Attempts to merge adjacent triangles into quads where geometrically feasible,
/// falling back to triangle representation for remaining faces.
/// </summary>
public class QuadMeshToObjExporter
{
    /// <summary>
    /// Cosine tolerance for determining if an angle is approximately 90 degrees.
    /// Value of 0.15 corresponds to angles between ~81° and ~99°.
    /// </summary>
    private const double RIGHT_ANGLE_COSINE_TOLERANCE = 0.15;

    /// <summary>
    /// OBJ format uses 1-based vertex indexing, while internal mesh uses 0-based.
    /// </summary>
    private const int OBJ_INDEX_OFFSET = 1;

    /// <summary>
    /// Number of vertices that define a quad face.
    /// </summary>
    private const int QUAD_VERTEX_COUNT = 4;

    /// <summary>
    /// Number of edges in a triangle.
    /// </summary>
    private const int TRIANGLE_EDGE_COUNT = 3;

    /// <summary>
    /// Expected number of shared vertices between two triangles forming a quad.
    /// </summary>
    private const int EXPECTED_SHARED_VERTICES = 2;

    /// <summary>
    /// Exports the specified mesh to an OBJ file, converting adjacent triangles 
    /// to quads where possible while preserving remaining triangles.
    /// </summary>
    /// <param name="mesh">The mesh to export containing triangle faces.</param>
    /// <param name="outputFilePath">Full path where the OBJ file will be created.</param>
    /// <exception cref="ArgumentNullException">Thrown when mesh or outputFilePath is null.</exception>
    /// <exception cref="IOException">Thrown when file cannot be written.</exception>
    public void ExportToFile(DMesh3 mesh, string outputFilePath)
    {
        ValidateInputParameters(mesh, outputFilePath);

        var objContent = new StringBuilder();

        WriteVertexData(mesh, objContent);
        WriteNormalData(mesh, objContent);

        var quadCandidates = FindQuadCandidates(mesh);
        var trianglesUsedInQuads = new HashSet<int>();

        WriteQuadFaces(objContent, quadCandidates, trianglesUsedInQuads);
        WriteRemainingTriangleFaces(mesh, objContent, trianglesUsedInQuads);

        File.WriteAllText(outputFilePath, objContent.ToString());
    }

    /// <summary>
    /// Validates input parameters for the export operation.
    /// </summary>
    /// <param name="mesh">Mesh to validate.</param>
    /// <param name="outputFilePath">File path to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when parameters are null or empty.</exception>
    private static void ValidateInputParameters(DMesh3 mesh, string outputFilePath)
    {
        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            throw new ArgumentNullException(nameof(outputFilePath));
        }
    }

    /// <summary>
    /// Writes all vertex positions to the OBJ content buffer.
    /// </summary>
    /// <param name="mesh">Source mesh containing vertex data.</param>
    /// <param name="objContent">StringBuilder to append vertex data to.</param>
    private void WriteVertexData(DMesh3 mesh, StringBuilder objContent)
    {
        foreach (int vertexId in mesh.VertexIndices())
        {
            Vector3d vertex = mesh.GetVertex(vertexId);
            objContent.AppendLine(string.Format("v {0:F6} {1:F6} {2:F6}", vertex.x, vertex.y, vertex.z));
        }
    }

    /// <summary>
    /// Writes all vertex normals to the OBJ content buffer.
    /// </summary>
    /// <param name="mesh">Source mesh containing normal data.</param>
    /// <param name="objContent">StringBuilder to append normal data to.</param>
    private void WriteNormalData(DMesh3 mesh, StringBuilder objContent)
    {
        foreach (int vertexId in mesh.VertexIndices())
        {
            Vector3d normal = mesh.GetVertexNormal(vertexId);
            objContent.AppendLine(string.Format("vn {0:F6} {1:F6} {2:F6}", normal.x, normal.y, normal.z));
        }
    }

    /// <summary>
    /// Writes quad faces to the OBJ content and tracks which triangles were used.
    /// </summary>
    /// <param name="objContent">StringBuilder to append quad face data to.</param>
    /// <param name="quadCandidates">List of validated quad faces to write.</param>
    /// <param name="trianglesUsedInQuads">Set to track triangle IDs used in quads.</param>
    private void WriteQuadFaces(StringBuilder objContent, List<QuadFaceInfo> quadCandidates, HashSet<int> trianglesUsedInQuads)
    {
        foreach (QuadFaceInfo quad in quadCandidates)
        {
            int[] objVertexIndices = ConvertToObjIndices(quad.VertexIndices);
            objContent.AppendLine($"f {objVertexIndices[0]} {objVertexIndices[1]} {objVertexIndices[2]} {objVertexIndices[3]}");

            trianglesUsedInQuads.Add(quad.SourceTriangleA);
            trianglesUsedInQuads.Add(quad.SourceTriangleB);
        }
    }

    /// <summary>
    /// Writes remaining triangle faces that couldn't be converted to quads.
    /// </summary>
    /// <param name="mesh">Source mesh containing triangle data.</param>
    /// <param name="objContent">StringBuilder to append triangle face data to.</param>
    /// <param name="trianglesUsedInQuads">Set of triangle IDs already used in quads.</param>
    private void WriteRemainingTriangleFaces(DMesh3 mesh, StringBuilder objContent, HashSet<int> trianglesUsedInQuads)
    {
        foreach (int triangleId in mesh.TriangleIndices())
        {
            if (trianglesUsedInQuads.Contains(triangleId))
            {
                continue;
            }

            Index3i triangleVertices = mesh.GetTriangle(triangleId);
            int[] objVertexIndices = ConvertToObjIndices(triangleVertices.array);
            objContent.AppendLine($"f {objVertexIndices[0]} {objVertexIndices[1]} {objVertexIndices[2]}");
        }
    }

    /// <summary>
    /// Converts 0-based mesh indices to 1-based OBJ indices.
    /// </summary>
    /// <param name="meshIndices">Array of 0-based vertex indices.</param>
    /// <returns>Array of 1-based vertex indices for OBJ format.</returns>
    private int[] ConvertToObjIndices(int[] meshIndices)
    {
        return meshIndices.Select(index => index + OBJ_INDEX_OFFSET).ToArray();
    }

    /// <summary>
    /// Analyzes the mesh to find all pairs of adjacent triangles that can form valid quads.
    /// Uses a greedy approach to maximize quad count while ensuring each triangle is used at most once.
    /// </summary>
    /// <param name="mesh">Source mesh to analyze.</param>
    /// <returns>List of valid quad candidates.</returns>
    private List<QuadFaceInfo> FindQuadCandidates(DMesh3 mesh)
    {
        var validQuads = new List<QuadFaceInfo>();
        var processedTriangles = new HashSet<int>();

        foreach (int triangleId in mesh.TriangleIndices())
        {
            if (processedTriangles.Contains(triangleId))
            {
                continue;
            }

            QuadFaceInfo? potentialQuad = FindQuadPartnerForTriangle(mesh, triangleId, processedTriangles);

            if (potentialQuad != null)
            {
                validQuads.Add(potentialQuad);
                processedTriangles.Add(potentialQuad.SourceTriangleA);
                processedTriangles.Add(potentialQuad.SourceTriangleB);
            }
        }

        return validQuads;
    }

    /// <summary>
    /// Attempts to find a neighboring triangle that can form a quad with the given triangle.
    /// </summary>
    /// <param name="mesh">Source mesh.</param>
    /// <param name="triangleId">ID of triangle to find a partner for.</param>
    /// <param name="processedTriangles">Set of triangles already processed.</param>
    /// <returns>Valid quad if found, null otherwise.</returns>
    private QuadFaceInfo? FindQuadPartnerForTriangle(DMesh3 mesh, int triangleId, HashSet<int> processedTriangles)
    {
        Index3i triangleVertices = mesh.GetTriangle(triangleId);

        for (int edgeIndex = 0; edgeIndex < TRIANGLE_EDGE_COUNT; edgeIndex++)
        {
            int currentVertex = triangleVertices[edgeIndex];
            int nextVertex = triangleVertices[(edgeIndex + 1) % TRIANGLE_EDGE_COUNT];
            var edge = new Edge { StartVertex = currentVertex, EndVertex = nextVertex };

            int? neighborTriangleId = FindNeighboringTriangle(mesh, triangleId, edge);

            if (neighborTriangleId.HasValue && !processedTriangles.Contains(neighborTriangleId.Value))
            {
                QuadFaceInfo? potentialQuad = TryCreateQuadFromTrianglePair(mesh, triangleId, neighborTriangleId.Value);

                if (potentialQuad != null)
                {
                    return potentialQuad;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the triangle that shares an edge with the given triangle.
    /// </summary>
    /// <param name="mesh">Source mesh.</param>
    /// <param name="triangleId">ID of the source triangle.</param>
    /// <param name="edge">Source triangle edge.</param>
    /// <returns>ID of neighboring triangle, or null if not found.</returns>
    private int? FindNeighboringTriangle(DMesh3 mesh, int triangleId, Edge edge)
    {
        int edgeId = mesh.FindEdge(edge.StartVertex, edge.EndVertex);

        if (edgeId == DMesh3.InvalidID)
        {
            return null;
        }

        Index2i connectedTriangles = mesh.GetEdgeT(edgeId);
        int neighborId = (connectedTriangles.a == triangleId) ? connectedTriangles.b : connectedTriangles.a;

        return neighborId != DMesh3.InvalidID ? neighborId : null;
    }

    /// <summary>
    /// Attempts to create a valid quad from two adjacent triangles.
    /// Validates that the resulting quad has approximately rectangular geometry.
    /// </summary>
    /// <param name="mesh">Source mesh.</param>
    /// <param name="triangleA">ID of first triangle.</param>
    /// <param name="triangleB">ID of second triangle.</param>
    /// <returns>Valid quad if geometry is rectangular, null otherwise.</returns>
    private QuadFaceInfo? TryCreateQuadFromTrianglePair(DMesh3 mesh, int triangleA, int triangleB)
    {
        Index3i verticesA = mesh.GetTriangle(triangleA);
        Index3i verticesB = mesh.GetTriangle(triangleB);

        var sharedVertices = FindSharedVertices(verticesA, verticesB);

        if (sharedVertices.Count != EXPECTED_SHARED_VERTICES)
        {
            return null;
        }

        int uniqueVertexA = FindUniqueVertex(verticesA, sharedVertices);
        int uniqueVertexB = FindUniqueVertex(verticesB, sharedVertices);

        var quadVertices = new List<int> { sharedVertices[0], uniqueVertexA, sharedVertices[1], uniqueVertexB };
        Vector3d triangleNormal = mesh.GetTriNormal(triangleA);

        int[] orderedVertices = OrderVerticesCounterClockwise(mesh, quadVertices, triangleNormal);

        if (!IsQuadGeometryRectangular(mesh, orderedVertices))
        {
            return null;
        }

        return new QuadFaceInfo
        {
            VertexIndices = orderedVertices,
            SourceTriangleA = triangleA,
            SourceTriangleB = triangleB
        };
    }

    /// <summary>
    /// Finds vertices that are common to both triangles.
    /// </summary>
    /// <param name="triangleA">Vertices of first triangle.</param>
    /// <param name="triangleB">Vertices of second triangle.</param>
    /// <returns>List of shared vertex indices.</returns>
    private List<int> FindSharedVertices(Index3i triangleA, Index3i triangleB)
    {
        return triangleA.array.Intersect(triangleB.array).ToList();
    }

    /// <summary>
    /// Finds the vertex that is unique to a triangle (not shared with another triangle).
    /// </summary>
    /// <param name="triangleVertices">Vertices of the triangle.</param>
    /// <param name="sharedVertices">Vertices shared with another triangle.</param>
    /// <returns>The unique vertex index.</returns>
    private int FindUniqueVertex(Index3i triangleVertices, List<int> sharedVertices)
    {
        return triangleVertices.array.Except(sharedVertices).Single();
    }

    /// <summary>
    /// Orders four vertices in counter-clockwise order relative to the surface normal.
    /// This ensures consistent face orientation in the output OBJ file.
    /// </summary>
    /// <param name="mesh">Source mesh for vertex position lookup.</param>
    /// <param name="vertices">List of 4 vertex indices to order.</param>
    /// <param name="surfaceNormal">Normal vector of the surface containing the quad.</param>
    /// <returns>Array of vertex indices in counter-clockwise order.</returns>
    private int[] OrderVerticesCounterClockwise(DMesh3 mesh, List<int> vertices, Vector3d surfaceNormal)
    {
        if (vertices.Count != QUAD_VERTEX_COUNT)
        {
            return vertices.ToArray();
        }

        Vector3d quadCenter = CalculateQuadCenter(mesh, vertices);
        Vector3d tangentU = CreateStableTangentVector(surfaceNormal);
        Vector3d tangentV = surfaceNormal.Cross(tangentU).Normalized;
        var quadOrientation = new QuadOrientation
        {
            CenterPoint = quadCenter,
            TangentU = tangentU,
            TangentV = tangentV
        };

        return vertices
            .OrderBy(vertexId => CalculateAngleFromCenter(mesh, vertexId, quadOrientation))
            .ToArray();
    }

    /// <summary>
    /// Calculates the geometric center of a quad.
    /// </summary>
    /// <param name="mesh">Source mesh for vertex position lookup.</param>
    /// <param name="vertices">List of vertex indices defining the quad.</param>
    /// <returns>3D position of the quad center.</returns>
    private Vector3d CalculateQuadCenter(DMesh3 mesh, List<int> vertices)
    {
        Vector3d center = Vector3d.Zero;

        foreach (int vertexId in vertices)
        {
            center += mesh.GetVertex(vertexId);
        }

        return center / vertices.Count;
    }

    /// <summary>
    /// Calculates the angle of a vertex relative to the quad center in 2D projection.
    /// </summary>
    /// <param name="mesh">Source mesh for vertex position lookup.</param>
    /// <param name="vertexId">ID of vertex to calculate angle for.</param>
    /// <param name="center">Center point of the quad.</param>
    /// <param name="tangentU">First tangent vector for 2D projection.</param>
    /// <param name="tangentV">Second tangent vector for 2D projection.</param>
    /// <returns>Angle in radians from the positive U-axis.</returns>
    private double CalculateAngleFromCenter(DMesh3 mesh, int vertexId, QuadOrientation quadOrientation)
    {
        Vector3d directionFromCenter = mesh.GetVertex(vertexId) - quadOrientation.CenterPoint;
        double projectedX = directionFromCenter.Dot(quadOrientation.TangentU);
        double projectedY = directionFromCenter.Dot(quadOrientation.TangentV);

        return Math.Atan2(projectedY, projectedX);
    }

    /// <summary>
    /// Creates a stable tangent vector perpendicular to the given normal.
    /// Uses different reference axes to avoid numerical instability.
    /// </summary>
    /// <param name="normal">Surface normal vector.</param>
    /// <returns>Normalized tangent vector perpendicular to the normal.</returns>
    private Vector3d CreateStableTangentVector(Vector3d normal)
    {
        // Try Y-axis first
        Vector3d candidate = normal.Cross(Vector3d.AxisY);

        if (candidate.LengthSquared > MathUtil.Epsilon)
        {
            return candidate.Normalized;
        }

        // Try X-axis if Y-axis is parallel
        candidate = normal.Cross(Vector3d.AxisX);

        if (candidate.LengthSquared > MathUtil.Epsilon)
        {
            return candidate.Normalized;
        }

        // Fall back to Z-axis
        return normal.Cross(Vector3d.AxisZ).Normalized;
    }

    /// <summary>
    /// Validates that four vertices form an approximately rectangular quad.
    /// Checks that all internal angles are close to 90 degrees within tolerance.
    /// </summary>
    /// <param name="mesh">Source mesh for vertex position lookup.</param>
    /// <param name="vertices">Array of 4 vertex indices in order.</param>
    /// <returns>True if the quad is approximately rectangular, false otherwise.</returns>
    private bool IsQuadGeometryRectangular(DMesh3 mesh, int[] vertices)
    {
        if (vertices.Length != QUAD_VERTEX_COUNT)
        {
            return false;
        }

        Vector3d[] vertexPositions = vertices.Select(mesh.GetVertex).ToArray();

        for (int i = 0; i < QUAD_VERTEX_COUNT; i++)
        {
            Vector3d previousVertex = vertexPositions[(i + 3) % QUAD_VERTEX_COUNT];
            Vector3d currentVertex = vertexPositions[i];
            Vector3d nextVertex = vertexPositions[(i + 1) % QUAD_VERTEX_COUNT];

            if (!IsValidQuadVertex(previousVertex, currentVertex, nextVertex))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates a single vertex of a quad by checking if its internal angle is approximately 90 degrees.
    /// </summary>
    /// <param name="previousVertex">Position of the previous vertex in the quad.</param>
    /// <param name="currentVertex">Position of the current vertex being validated.</param>
    /// <param name="nextVertex">Position of the next vertex in the quad.</param>
    /// <returns>True if the vertex forms a valid right angle, false otherwise.</returns>
    private bool IsValidQuadVertex(Vector3d previousVertex, Vector3d currentVertex, Vector3d nextVertex)
    {
        Vector3d edgeToPrevious = previousVertex - currentVertex;
        Vector3d edgeToNext = nextVertex - currentVertex;

        // Check for degenerate edges (too short)
        if (edgeToPrevious.LengthSquared < MathUtil.Epsilon || edgeToNext.LengthSquared < MathUtil.Epsilon)
        {
            return false;
        }

        Vector3d directionToPrevious = edgeToPrevious.Normalized;
        Vector3d directionToNext = edgeToNext.Normalized;

        // For a right angle, the dot product should be close to 0
        double angleCosine = directionToPrevious.Dot(directionToNext);

        return Math.Abs(angleCosine) <= RIGHT_ANGLE_COSINE_TOLERANCE;
    }

    /// <summary>
    /// Represents a quadrilateral face composed of two adjacent triangles.
    /// Contains the ordered vertex indices and references to the source triangles.
    /// </summary>
    private sealed class QuadFaceInfo
    {
        /// <summary>
        /// Array of 4 vertex indices in counter-clockwise order.
        /// </summary>
        public int[] VertexIndices { get; set; } = [];

        /// <summary>
        /// ID of the first source triangle that forms this quad.
        /// </summary>
        public int SourceTriangleA { get; set; }

        /// <summary>
        /// ID of the second source triangle that forms this quad.
        /// </summary>
        public int SourceTriangleB { get; set; }
    }

    /// <summary>
    /// Represents the orientation and projection of a quad in 3D space.
    /// </summary>
    private sealed class QuadOrientation
    {
        /// <summary>
        /// Center point of the quad.
        /// </summary>
        public Vector3d CenterPoint { get; set; }

        /// <summary>
        /// First tangent vector for 2D projection.
        /// </summary>
        public Vector3d TangentU { get; set; }

        /// <summary>
        /// Second tangent vector for 2D projection.
        /// </summary>
        public Vector3d TangentV { get; set; }
    }

    /// <summary>
    /// Represents an edge connecting two vertices in the mesh.
    /// </summary>
    private sealed class Edge
    {
        /// <summary>
        /// Start vertex index of the edge.
        /// </summary>
        public int StartVertex { get; set; }

        /// <summary>
        /// End vertex index of the edge.
        /// </summary>
        public int EndVertex { get; set; }
    }
}
