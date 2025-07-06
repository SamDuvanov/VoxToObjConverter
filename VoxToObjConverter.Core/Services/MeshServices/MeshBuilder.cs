using g3;
using VoxReader;

namespace VoxToObjConverter.Core.Services.MeshServices;

public class MeshBuilder
{
    public DMesh3 GenerateSolidBoxyMesh(IEnumerable<Voxel> voxels)
    {
        var mesh = new DMesh3();
        var voxelList = voxels.ToList();

        if (!voxelList.Any())
            return mesh;

        // Создаем хеш-сет для быстрого поиска вокселей
        var voxelSet = new HashSet<(int x, int y, int z)>(
            voxelList.Select(v => ((int)v.LocalPosition.X, (int)v.LocalPosition.Y, (int)v.LocalPosition.Z))
        );

        // Определяем границы модели
        var bounds = GetModelBounds(voxelList);
        var expandedBounds = (
            minX: bounds.minX - 1,
            maxX: bounds.maxX + 1,
            minY: bounds.minY - 1,
            maxY: bounds.maxY + 1,
            minZ: bounds.minZ - 1,
            maxZ: bounds.maxZ + 1
        );

        // Находим внешнее пространство через flood fill
        var externalSpace = GetExternalSpace(voxelSet, expandedBounds);

        foreach (var voxel in voxelList)
        {
            AddVoxelFaces(mesh, voxel, voxelSet, externalSpace);
        }

        return mesh;
    }

    private (int minX, int maxX, int minY, int maxY, int minZ, int maxZ) GetModelBounds(IEnumerable<Voxel> voxels)
    {
        var positions = voxels.Select(v => v.LocalPosition).ToList();
        return (
            minX: positions.Min(p => p.X),
            maxX: positions.Max(p => p.X),
            minY: positions.Min(p => p.Y),
            maxY: positions.Max(p => p.Y),
            minZ: positions.Min(p => p.Z),
            maxZ: positions.Max(p => p.Z)
        );
    }

    private HashSet<(int x, int y, int z)> GetExternalSpace(HashSet<(int x, int y, int z)> voxelSet,
        (int minX, int maxX, int minY, int maxY, int minZ, int maxZ) bounds)
    {
        var externalSpace = new HashSet<(int x, int y, int z)>();
        var queue = new Queue<(int x, int y, int z)>();
        var visited = new HashSet<(int x, int y, int z)>();

        // Начинаем flood fill от угла расширенных границ
        var startPoint = (bounds.minX, bounds.minY, bounds.minZ);
        queue.Enqueue(startPoint);
        visited.Add(startPoint);

        var directions = new[]
        {
            (-1, 0, 0), (1, 0, 0),
            (0, -1, 0), (0, 1, 0),
            (0, 0, -1), (0, 0, 1)
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            externalSpace.Add(current);

            foreach (var (dx, dy, dz) in directions)
            {
                var next = (current.x + dx, current.y + dy, current.z + dz);

                // Проверяем границы
                if (next.Item1 < bounds.minX || next.Item1 > bounds.maxX ||
                    next.Item2 < bounds.minY || next.Item2 > bounds.maxY ||
                    next.Item3 < bounds.minZ || next.Item3 > bounds.maxZ)
                    continue;

                // Пропускаем уже посещенные или занятые вокселями позиции
                if (visited.Contains(next) || voxelSet.Contains(next))
                    continue;

                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return externalSpace;
    }

    private void AddVoxelFaces(DMesh3 mesh, Voxel voxel, HashSet<(int x, int y, int z)> voxelSet,
        HashSet<(int x, int y, int z)> externalSpace)
    {
        var x = (int)voxel.LocalPosition.X;
        var y = (int)voxel.LocalPosition.Y;
        var z = (int)voxel.LocalPosition.Z;

        // Определяем 8 вершин куба
        var v000 = new Vector3d(x, y, z);         // 0,0,0
        var v001 = new Vector3d(x, y, z + 1);     // 0,0,1
        var v010 = new Vector3d(x, y + 1, z);     // 0,1,0
        var v011 = new Vector3d(x, y + 1, z + 1); // 0,1,1
        var v100 = new Vector3d(x + 1, y, z);     // 1,0,0
        var v101 = new Vector3d(x + 1, y, z + 1); // 1,0,1
        var v110 = new Vector3d(x + 1, y + 1, z); // 1,1,0
        var v111 = new Vector3d(x + 1, y + 1, z + 1); // 1,1,1

        // Добавляем грани только если соседняя позиция является внешним пространством

        // Передняя грань (Z-)
        var neighborZMinus = (x, y, z - 1);
        if (!voxelSet.Contains(neighborZMinus) && externalSpace.Contains(neighborZMinus))
        {
            AddQuad(mesh, v000, v010, v110, v100);
        }

        // Задняя грань (Z+)
        var neighborZPlus = (x, y, z + 1);
        if (!voxelSet.Contains(neighborZPlus) && externalSpace.Contains(neighborZPlus))
        {
            AddQuad(mesh, v101, v111, v011, v001);
        }

        // Левая грань (X-)
        var neighborXMinus = (x - 1, y, z);
        if (!voxelSet.Contains(neighborXMinus) && externalSpace.Contains(neighborXMinus))
        {
            AddQuad(mesh, v001, v011, v010, v000);
        }

        // Правая грань (X+)
        var neighborXPlus = (x + 1, y, z);
        if (!voxelSet.Contains(neighborXPlus) && externalSpace.Contains(neighborXPlus))
        {
            AddQuad(mesh, v100, v110, v111, v101);
        }

        // Нижняя грань (Y-)
        var neighborYMinus = (x, y - 1, z);
        if (!voxelSet.Contains(neighborYMinus) && externalSpace.Contains(neighborYMinus))
        {
            AddQuad(mesh, v000, v100, v101, v001);
        }

        // Верхняя грань (Y+)
        var neighborYPlus = (x, y + 1, z);
        if (!voxelSet.Contains(neighborYPlus) && externalSpace.Contains(neighborYPlus))
        {
            AddQuad(mesh, v011, v111, v110, v010);
        }
    }

    private void AddQuad(DMesh3 mesh, Vector3d v0, Vector3d v1, Vector3d v2, Vector3d v3)
    {
        // Добавляем вершины
        var i0 = mesh.AppendVertex(v0);
        var i1 = mesh.AppendVertex(v1);
        var i2 = mesh.AppendVertex(v2);
        var i3 = mesh.AppendVertex(v3);

        // Добавляем два треугольника для квада
        mesh.AppendTriangle(i0, i1, i2);
        mesh.AppendTriangle(i0, i2, i3);
    }
}