using g3;
using VoxReader;
using VoxReader.Interfaces;

namespace VoxToObjConverter.Core.Services.MeshServices.Utils
{
    /// <summary>
    /// Utility class to apply VOX model transformations (rotation and translation)
    /// to its corresponding mesh using g3 geometry primitives.
    /// </summary>
    public class TransformMeshUtility
    {
        /// <summary>
        /// Applies the VOX model's global transformation to a mesh,
        /// including rotation and position adjustment.
        /// </summary>
        /// <param name="voxModel">The VOX model with transform data.</param>
        /// <param name="voxMesh">The mesh to be transformed.</param>
        public void ApplyTransformToMesh(IModel voxModel, DMesh3 voxMesh)
        {
            // Step 1: Calculate the final rotation by combining model and fix rotations.
            Quaterniond rotation = GetFinalRotation(voxModel);

            // Step 2: Convert model position into g3's coordinate space.
            Vector3d translation = GetTransformedPosition(voxModel);

            // Step 3: Apply rotation and translation to every mesh vertex.
            TransformMeshVertices(voxMesh, rotation, translation);
        }

        /// <summary>
        /// Combines the model's rotation with coordinate system fix rotation.
        /// </summary>
        private Quaterniond GetFinalRotation(IModel voxModel)
        {
            // Convert VOX matrix to quaternion
            Quaterniond modelRotation = ConvertMatrixToQuaternion(voxModel.GlobalRotation);

            // VOX uses Z-up, g3 uses Y-up → rotate -90° around X-axis
            Quaterniond coordinateFix = Quaterniond.AxisAngleD(Vector3d.AxisX, -90);

            // Final rotation = fix * model
            return coordinateFix * modelRotation;
        }

        /// <summary>
        /// Transforms the model's global position into the correct coordinate space.
        /// </summary>
        private Vector3d GetTransformedPosition(IModel voxModel)
        {
            // Extract original VOX position
            Vector3d originalPosition = new Vector3d(
                voxModel.GlobalPosition.X,
                voxModel.GlobalPosition.Y,
                voxModel.GlobalPosition.Z
            );

            // Apply -90° rotation to align with g3 coordinates
            Quaterniond coordinateFix = Quaterniond.AxisAngleD(Vector3d.AxisX, -90);

            // Rotate the position
            return coordinateFix * originalPosition;
        }

        /// <summary>
        /// Rotates and translates each vertex of the mesh.
        /// </summary>
        private void TransformMeshVertices(DMesh3 mesh, Quaterniond rotation, Vector3d translation)
        {
            for (int vid = 0; vid < mesh.VertexCount; vid++)
            {
                // Get original vertex position
                Vector3d vertex = mesh.GetVertex(vid);

                // Rotate the vertex
                Vector3d rotatedVertex = rotation * vertex;

                // Translate the rotated vertex
                Vector3d finalVertex = rotatedVertex + translation;

                // Update the mesh with new position
                mesh.SetVertex(vid, finalVertex);
            }
        }

        /// <summary>
        /// Converts a 3x3 rotation matrix into a quaternion.
        /// </summary>
        private static Quaterniond ConvertMatrixToQuaternion(Matrix3 matrix)
        {
            double trace = matrix[0, 0] + matrix[1, 1] + matrix[2, 2];
            double w, x, y, z;

            if (trace > 0)
            {
                // Case 1: Trace is positive
                double s = Math.Sqrt(trace + 1.0) * 2;
                w = 0.25 * s;
                x = (matrix[2, 1] - matrix[1, 2]) / s;
                y = (matrix[0, 2] - matrix[2, 0]) / s;
                z = (matrix[1, 0] - matrix[0, 1]) / s;
            }
            else if (matrix[0, 0] > matrix[1, 1] && matrix[0, 0] > matrix[2, 2])
            {
                // Case 2: matrix[0,0] is the dominant diagonal term
                double s = Math.Sqrt(1.0 + matrix[0, 0] - matrix[1, 1] - matrix[2, 2]) * 2;
                w = (matrix[2, 1] - matrix[1, 2]) / s;
                x = 0.25 * s;
                y = (matrix[0, 1] + matrix[1, 0]) / s;
                z = (matrix[0, 2] + matrix[2, 0]) / s;
            }
            else if (matrix[1, 1] > matrix[2, 2])
            {
                // Case 3: matrix[1,1] is the dominant diagonal term
                double s = Math.Sqrt(1.0 + matrix[1, 1] - matrix[0, 0] - matrix[2, 2]) * 2;
                w = (matrix[0, 2] - matrix[2, 0]) / s;
                x = (matrix[0, 1] + matrix[1, 0]) / s;
                y = 0.25 * s;
                z = (matrix[1, 2] + matrix[2, 1]) / s;
            }
            else
            {
                // Case 4: matrix[2,2] is the dominant diagonal term
                double s = Math.Sqrt(1.0 + matrix[2, 2] - matrix[0, 0] - matrix[1, 1]) * 2;
                w = (matrix[1, 0] - matrix[0, 1]) / s;
                x = (matrix[0, 2] + matrix[2, 0]) / s;
                y = (matrix[1, 2] + matrix[2, 1]) / s;
                z = 0.25 * s;
            }

            return new Quaterniond(x, y, z, w);
        }
    }
}
