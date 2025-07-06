using g3;
using gs;
using System;
using System.Collections.Generic;

namespace VoxToObjConverter.Core.Services.MeshServices;

public class MeshWelder
{
    private readonly DMesh3 _mesh;

    public MeshWelder(DMesh3 mesh)
    {
        this._mesh = mesh;
    }

    public void Weld()
    {
        // Этап 1: Удаление дубликатов треугольников
        var remDupe = new RemoveDuplicateTriangles(_mesh);
        remDupe.Apply();

        // Этап 2: Сливаем смежные рёбра (объединяем вершины)
        var mergeEdges = new MergeCoincidentEdges(_mesh);
        mergeEdges.Apply();

        // Этап 3: Исправление нормалей (основная причина черных треугольников)
        FixNormals();

        // Этап 4: Компактификация
        _mesh.CompactInPlace();

        // Этап 5: Проверка валидности
        _mesh.CheckValidity();
    }

    private void FixNormals()
    {
        // Если нормали есть, пересчитываем их
        if (_mesh.HasVertexNormals)
        {
            Console.WriteLine("Пересчитываем существующие нормали");
            MeshNormals.QuickCompute(_mesh);
        }
        else
        {
            // Создаем нормали, если их нет
            Console.WriteLine("Создаем нормали для вершин");
            _mesh.EnableVertexNormals(Vector3f.AxisY);
            MeshNormals.QuickCompute(_mesh);
        }

        // Проверяем и исправляем нулевые нормали
        FixZeroNormals();

        // Опционально: исправляем ориентацию для закрытых мешей
        if (_mesh.IsClosed())
        {
            TryFixOrientation();
        }
    }

    private void FixZeroNormals()
    {
        int fixedCount = 0;

        foreach (int vid in _mesh.VertexIndices())
        {
            if (!_mesh.IsVertex(vid))
                continue;

            var normal = _mesh.GetVertexNormal(vid);

            // Если нормаль нулевая или очень маленькая
            if (normal.Length < 1e-6)
            {
                // Вычисляем нормаль на основе соседних треугольников
                var newNormal = ComputeVertexNormal(vid);
                if (newNormal.Length > 1e-6)
                {
                    _mesh.SetVertexNormal(vid, newNormal.Normalized);
                    fixedCount++;
                }
            }
        }

        if (fixedCount > 0)
        {
            Console.WriteLine($"Исправлено {fixedCount} нулевых нормалей");
        }
    }

    private Vector3f ComputeVertexNormal(int vid)
    {
        var normal = Vector3f.Zero;
        int count = 0;

        foreach (int tid in _mesh.VtxTrianglesItr(vid))
        {
            if (!_mesh.IsTriangle(tid))
                continue;

            var tri = _mesh.GetTriangle(tid);
            var v0 = _mesh.GetVertex(tri.a);
            var v1 = _mesh.GetVertex(tri.b);
            var v2 = _mesh.GetVertex(tri.c);

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var triNormal = edge1.Cross(edge2);

            if (triNormal.Length > 1e-8)
            {
                var normalizedTriNormal = triNormal.Normalized;
                normal += new Vector3f((float)normalizedTriNormal.x, (float)normalizedTriNormal.y, (float)normalizedTriNormal.z);
                count++;
            }
        }

        return count > 0 ? normal / count : Vector3f.AxisY;
    }

    private void TryFixOrientation()
    {
        // Очень консервативное исправление ориентации
        // Только для явно неправильных треугольников
        int flippedCount = 0;
        var trianglesToCheck = new List<int>();

        // Сначала собираем статистику по ориентации
        int outwardCount = 0;
        int inwardCount = 0;

        foreach (int tid in _mesh.TriangleIndices())
        {
            if (!_mesh.IsTriangle(tid))
                continue;

            var tri = _mesh.GetTriangle(tid);
            var v0 = _mesh.GetVertex(tri.a);
            var v1 = _mesh.GetVertex(tri.b);
            var v2 = _mesh.GetVertex(tri.c);

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var normal = edge1.Cross(edge2);

            if (normal.Length < 1e-8)
                continue;

            var center = (v0 + v1 + v2) / 3.0;

            // Простая эвристика для определения направления
            if (Vector3d.Dot(normal.Normalized, center.Normalized) > 0)
            {
                outwardCount++;
            }
            else
            {
                inwardCount++;
                trianglesToCheck.Add(tid);
            }
        }

        // Переворачиваем только если явное меньшинство треугольников направлено внутрь
        if (inwardCount < outwardCount / 4) // Менее 25% от общего количества
        {
            foreach (int tid in trianglesToCheck)
            {
                if (_mesh.IsTriangle(tid))
                {
                    _mesh.ReverseTriOrientation(tid);
                    flippedCount++;
                }
            }
        }

        if (flippedCount > 0)
        {
            Console.WriteLine($"Исправлена ориентация для {flippedCount} треугольников");
            // Пересчитываем нормали после изменения ориентации
            MeshNormals.QuickCompute(_mesh);
        }
    }

    // Диагностический метод
    public void DiagnoseMesh()
    {
        Console.WriteLine($"=== Диагностика меша ===");
        Console.WriteLine($"Количество вершин: {_mesh.VertexCount}");
        Console.WriteLine($"Количество треугольников: {_mesh.TriangleCount}");
        Console.WriteLine($"Меш закрыт: {_mesh.IsClosed()}");
        Console.WriteLine($"Есть нормали: {_mesh.HasVertexNormals}");

        if (_mesh.HasVertexNormals)
        {
            int zeroNormals = 0;
            foreach (int vid in _mesh.VertexIndices())
            {
                if (!_mesh.IsVertex(vid))
                    continue;

                var normal = _mesh.GetVertexNormal(vid);
                if (normal.Length < 1e-6)
                {
                    zeroNormals++;
                }
            }
            Console.WriteLine($"Нулевых нормалей: {zeroNormals}");
        }

        // Проверка на вырожденные треугольники
        int degenerateCount = 0;
        foreach (int tid in _mesh.TriangleIndices())
        {
            if (!_mesh.IsTriangle(tid))
                continue;

            var tri = _mesh.GetTriangle(tid);
            var v0 = _mesh.GetVertex(tri.a);
            var v1 = _mesh.GetVertex(tri.b);
            var v2 = _mesh.GetVertex(tri.c);

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var cross = edge1.Cross(edge2);

            if (cross.Length < 1e-8)
            {
                degenerateCount++;
            }
        }
        Console.WriteLine($"Вырожденных треугольников: {degenerateCount}");
        Console.WriteLine($"======================");
    }
}