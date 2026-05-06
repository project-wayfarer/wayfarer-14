using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;

namespace Content.Client.Lathe.UI;

public sealed partial class LatheMenu
{
    /// <summary>
    /// Stores the current buffer amount
    /// </summary>
    private int? _bufferAmount;
    /// <summary>
    /// Updates the biomass buffer and refreshes the recipe list
    /// </summary>
    public void UpdateBuffer(int? buffer)
    {
        BufferContainer.Visible = buffer.HasValue;
        _bufferAmount = buffer; // store for later use
        if (buffer.HasValue)
            BufferLabel.Text = buffer.Value.ToString();
        PopulateRecipes(); // re‑evaluate recipe availability
    }
    /// <summary>
    /// Checks availability including the buffer
    /// </summary>
    private bool CanProduceWithBuffer(LatheRecipePrototype recipe, int quantity)
    {
        if (!_entityManager.TryGetComponent(Entity, out LatheComponent? lathe))
            return false;

        foreach (var (material, amount) in recipe.Materials)
        {
            var required = SharedLatheSystem.AdjustMaterial(amount, recipe.ApplyMaterialDiscount, lathe.FinalMaterialUseMultiplier) * quantity;

            if (material == "Biomass")
            {
                var stored = _materialStorage.GetMaterialAmount(Entity, material);
                var total = stored + (_bufferAmount ?? 0);
                if (total < required)
                    return false;
            }
            else
            {
                var available = _materialStorage.GetMaterialAmount(Entity, material);
                if (available < required)
                    return false;
            }
        }
        return true;
    }
    /// <summary>
    /// Gets the total available amount of a material, including buffer contributions for biomass.
    /// </summary>
    private int GetTotalMaterialAmount(string materialId, int? bufferAmount)
    {
        var stored = _materialStorage.GetMaterialAmount(Entity, materialId);
        if (materialId == "Biomass" && bufferAmount.HasValue)
            return stored + bufferAmount.Value;
        return stored;
    }
}
