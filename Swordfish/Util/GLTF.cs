namespace Swordfish.Util;

public static class GLTF
{
    public static void Load()
    {
        glTFLoader.Schema.Gltf gltf = glTFLoader.Interface.LoadModel("PathToModel.gltf");
    }
}
