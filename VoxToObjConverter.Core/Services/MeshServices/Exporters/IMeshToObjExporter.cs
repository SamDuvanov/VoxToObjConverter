using g3;

namespace VoxToObjConverter.Core.Services.MeshServices.Exporters
{
    public interface IMeshToObjExporter
    {
        void ExportToFile(DMesh3 mesh, string filePath);
    }
}