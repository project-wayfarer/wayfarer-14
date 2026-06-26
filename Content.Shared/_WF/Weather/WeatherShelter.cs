namespace Content.Shared._WF.Weather;

// Marks a weather as particles that walls and roofs stop. Rain, snow, ash, hail, etc.
// Damage (if any) only hits mobs on a tile open to the sky.
[DataDefinition]
public sealed partial class WeatherParticulate
{
}

// Marks a weather as something that fills any space, like gas or radiation. Only stopped
// by a sealed, pressurised tile. Default when a weather declares neither shelter.
[DataDefinition]
public sealed partial class WeatherPermeating
{
}
