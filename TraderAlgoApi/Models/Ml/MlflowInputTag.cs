using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

/// <summary>Composite PK: (input_uuid, name) — configure with HasKey() in DbContext.</summary>
[Table("input_tags", Schema = "mlflow")]
public sealed class MlflowInputTag
{
    [Column("input_uuid")]
    public string InputUuid { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("value")]
    public string Value { get; set; } = null!;
}
