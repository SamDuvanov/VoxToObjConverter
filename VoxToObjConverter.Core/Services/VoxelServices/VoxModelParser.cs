using VoxReader.Interfaces;

namespace VoxToObjConverter.Core.Services.VoxelServices
{
    public class VoxModelParser
    {
        public IModel[] ReadModel(string voxFilePath)
        {
            var voxFile = VoxReader.VoxReader.Read(voxFilePath);

            return voxFile.Models;
        }
    }
}
