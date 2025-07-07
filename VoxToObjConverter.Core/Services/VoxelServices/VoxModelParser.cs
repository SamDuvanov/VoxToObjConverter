using VoxReader.Interfaces;

namespace VoxToObjConverter.Core.Services.VoxelServices
{
    public class VoxModelParser
    {
        public IModel ReadModel()
        {
            var voxFile = VoxReader.VoxReader.Read("Vox Models/teapot.vox");

            return voxFile.Models[0];
        }
    }
}
