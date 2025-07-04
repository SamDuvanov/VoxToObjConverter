using VoxReader;
using VoxReader.Interfaces;

namespace VoxToObjConverter.Core.Services.VoxelServices
{
    public class VoxParser
    {
        public IModel ReadModel()
        {
            var voxFile = VoxReader.VoxReader.Read("Vox Models/model.vox");

            return voxFile.Models[0];
        }
    }
}
