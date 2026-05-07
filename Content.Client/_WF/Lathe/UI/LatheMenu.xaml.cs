using System.Linq;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;

namespace Content.Client.Lathe.UI;

public sealed partial class LatheMenu
{
    /// <summary>
    /// Returns the maximum number of times a given recipe can be crafted.
    /// Useful for crafting as many of something as possible
    /// </summary>
    /// <param name="recipe"></param> The recipe to check crafting limit for
    /// <returns></returns> An integer representing the maximum number of times the recipe can be crafted
    private int GetMaximumCraftQuantity(LatheRecipePrototype recipe) // Wayfarer
    {
        if (!_entityManager.TryGetComponent(Entity, out LatheComponent? lathe))
            return 1;

        var maxPerMaterial = new List<int>();

        foreach (var (material, amount) in recipe.Materials)
        {
            var cost = SharedLatheSystem.AdjustMaterial(amount,
                recipe.ApplyMaterialDiscount,
                lathe.FinalMaterialUseMultiplier);
            maxPerMaterial.Add(GetTotalMaterialAmount(material, _bufferAmount ?? 0) / cost);
        }

        return maxPerMaterial.Min();
    }
}
