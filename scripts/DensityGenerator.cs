using Godot;

public class DensityGenerator : Node
{
    private OpenSimplexNoise noise;

    [Export] Vector3 scale        = new Vector3(1, 1, 1);
    [Export] int octaves          = 4;
    [Export] float period         = 20.0f;
    [Export] float persistence    = 0.8f;
    [Export] float altitudeFactor = 0.1f;
    [Export] int seed             = 1;

    public DensityGenerator()
    {
        noise = new OpenSimplexNoise();
    }

    public override void _Ready()
    {
        base._Ready();
        UpdateNoiseFunction();
    }

    public override bool _Set(string property, object value)
    {
        bool existed = base._Set(property, value);
        if (existed) UpdateNoiseFunction();
        return existed;
    }

    public float GetDensity(Vector3 at)
    {
        var value = noise.GetNoise3dv(at * scale) - at.y * altitudeFactor;
        if (value < 0.0f) value = 0.0f;
        else if (value > 1.0f) value = 1.0f;
        return value;
    }

    private void UpdateNoiseFunction()
    {
        noise.Octaves     = octaves;
        noise.Period      = period;
        noise.Persistence = persistence;
        noise.Seed        = seed;
    }
}