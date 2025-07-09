using g3;
using VoxReader;
using VoxReader.Interfaces;

namespace VoxToObjConverter.Core.Services.MeshServices.Utils
{
    public class TransformMeshUtility
    {
        public void TransformVoxModelMesh(IModel voxModel, DMesh3 voxModelMesh)
        {
            // 1. Поворот из VOX
            Quaterniond modelRotation = MatrixToQuaternion(voxModel.GlobalRotation);

            // 2. VOX → g3 (Z вверх → Y вверх)
            Quaterniond fixRotation = Quaterniond.AxisAngleD(Vector3d.AxisX, -90);

            // 3. Общий поворот
            Quaterniond finalRotation = fixRotation * modelRotation;

            // 4. Глобальная позиция
            Vector3d originalPos = new Vector3d(
                voxModel.GlobalPosition.X,
                voxModel.GlobalPosition.Y,
                voxModel.GlobalPosition.Z
            );

            Vector3d rotatedPos = fixRotation * originalPos;

            // 5. Центровка по Z — вычисляем bounding box из LocalPosition всех вокселей
            var voxels = voxModel.Voxels;
            double zMin = voxels.Min(v => v.LocalPosition.Z);
            double zMax = voxels.Max(v => v.LocalPosition.Z);
            double zCenter = (zMin + zMax) / 2.0;
            Vector3d centerOffset = new Vector3d(0, 0, zCenter);

            // 6. Трансформация вершин
            for (int vid = 0; vid < voxModelMesh.VertexCount; vid++)
            {
                Vector3d v = voxModelMesh.GetVertex(vid);
                Vector3d centered = v - centerOffset;
                Vector3d rotatedVertex = finalRotation * centered;
                voxModelMesh.SetVertex(vid, rotatedVertex + rotatedPos);
            }
        }

        private static Quaterniond MatrixToQuaternion(Matrix3 matrix)
        {
            double trace = matrix[0, 0] + matrix[1, 1] + matrix[2, 2];
            double w, x, y, z;

            if (trace > 0)
            {
                double s = Math.Sqrt(trace + 1.0) * 2; // s = 4 * qw
                w = 0.25 * s;
                x = (matrix[2, 1] - matrix[1, 2]) / s;
                y = (matrix[0, 2] - matrix[2, 0]) / s;
                z = (matrix[1, 0] - matrix[0, 1]) / s;
            }
            else if (matrix[0, 0] > matrix[1, 1] && matrix[0, 0] > matrix[2, 2])
            {
                double s = Math.Sqrt(1.0 + matrix[0, 0] - matrix[1, 1] - matrix[2, 2]) * 2; // s = 4 * qx
                w = (matrix[2, 1] - matrix[1, 2]) / s;
                x = 0.25 * s;
                y = (matrix[0, 1] + matrix[1, 0]) / s;
                z = (matrix[0, 2] + matrix[2, 0]) / s;
            }
            else if (matrix[1, 1] > matrix[2, 2])
            {
                double s = Math.Sqrt(1.0 + matrix[1, 1] - matrix[0, 0] - matrix[2, 2]) * 2; // s = 4 * qy
                w = (matrix[0, 2] - matrix[2, 0]) / s;
                x = (matrix[0, 1] + matrix[1, 0]) / s;
                y = 0.25 * s;
                z = (matrix[1, 2] + matrix[2, 1]) / s;
            }
            else
            {
                double s = Math.Sqrt(1.0 + matrix[2, 2] - matrix[0, 0] - matrix[1, 1]) * 2; // s = 4 * qz
                w = (matrix[1, 0] - matrix[0, 1]) / s;
                x = (matrix[0, 2] + matrix[2, 0]) / s;
                y = (matrix[1, 2] + matrix[2, 1]) / s;
                z = 0.25 * s;
            }

            return new Quaterniond(x, y, z, w);
        }
    }
}