namespace TraderAlgoApi.Services.Ml;

public sealed class MlflowOptions
{
    public bool Enabled { get; set; }

    public string? ConnectionString { get; set; }

    public string? TrackingUri { get; set; }
}
