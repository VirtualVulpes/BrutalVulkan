namespace BrutalVulkan;

public readonly record struct MaterialId(UInt32 Value);

public sealed class MaterialManager
{
    private UInt32 _nextId = 0;
    
    private readonly Dictionary<MaterialId, Material> _materials = new();
    private readonly Dictionary<String, MaterialId> _names = new();
    
    public MaterialId AddMaterial(String name, Material material)
    {
        var materialId = new MaterialId(_nextId++);
        
        _materials.Add(materialId, material);
        _names.Add(name, materialId);

        return materialId;
    }
    
    public Material GetMaterial(MaterialId materialId)
    {
        return _materials[materialId];
    }

    public MaterialId GetMaterialId(String name)
    {
        return _names[name];
    }
}