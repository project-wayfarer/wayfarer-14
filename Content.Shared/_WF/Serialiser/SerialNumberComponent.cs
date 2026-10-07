namespace Content.Shared._WF.Serialiser;

[RegisterComponent]
public sealed partial class SerialNumberComponent : Component
{
    [DataField]
    public string Serial = string.Empty;
}
