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
        // Удаление дубликатов треугольников
        var remDupe = new RemoveDuplicateTriangles(_mesh);
        remDupe.Apply();

        // Слияние смежных рёбер (не обязательно эффективно, но оставим)
        var mergeEdges = new MergeCoincidentEdges(_mesh);
        mergeEdges.Apply();

        // Компактификация
        _mesh.CompactInPlace();

        MeshNormals.QuickCompute(_mesh);

        // Проверка валидности
        _mesh.CheckValidity();
    }
}
