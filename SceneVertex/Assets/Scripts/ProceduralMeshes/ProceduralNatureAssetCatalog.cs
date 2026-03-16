using System.Collections.Generic;

public enum ProceduralNaturePropCategory
{
    Tree,
    Rock,
    Fence,
    Flower,
    Bush,
    Mushroom
}

public static class ProceduralNatureAssetCatalog
{
    public const string SharedMaterialAssetName = "SceneVertexShared";

    private static readonly ProceduralNaturePropCategory[] Categories =
    {
        ProceduralNaturePropCategory.Tree,
        ProceduralNaturePropCategory.Rock,
        ProceduralNaturePropCategory.Fence,
        ProceduralNaturePropCategory.Flower,
        ProceduralNaturePropCategory.Bush,
        ProceduralNaturePropCategory.Mushroom
    };

    private static readonly Dictionary<ProceduralNaturePropCategory, string[]> MeshAssetNames = new()
    {
        { ProceduralNaturePropCategory.Tree, new[] { "TreeA", "TreeB", "TreeC" } },
        { ProceduralNaturePropCategory.Rock, new[] { "RockA", "RockB", "RockC" } },
        { ProceduralNaturePropCategory.Fence, new[] { "Fence" } },
        { ProceduralNaturePropCategory.Flower, new[] { "FlowerA", "FlowerB", "FlowerC" } },
        { ProceduralNaturePropCategory.Bush, new[] { "BushA", "BushB", "BushC" } },
        { ProceduralNaturePropCategory.Mushroom, new[] { "MushroomA", "MushroomB", "MushroomC" } }
    };

    public static IEnumerable<ProceduralNaturePropCategory> GetAllCategories()
    {
        return Categories;
    }

    public static IReadOnlyList<string> GetMeshAssetNames(ProceduralNaturePropCategory category)
    {
        return MeshAssetNames[category];
    }

    public static IEnumerable<string> GetAllMeshAssetNames()
    {
        foreach (var category in Categories)
        {
            foreach (var assetName in MeshAssetNames[category])
            {
                yield return assetName;
            }
        }
    }

    public static IEnumerable<string> GetAllMaterialAssetNames()
    {
        yield return SharedMaterialAssetName;
    }

    public static string GetMaterialAssetName(ProceduralNaturePropCategory category)
    {
        return SharedMaterialAssetName;
    }

    public static int GetVariantCount(ProceduralNaturePropCategory category)
    {
        return MeshAssetNames[category].Length;
    }
}
