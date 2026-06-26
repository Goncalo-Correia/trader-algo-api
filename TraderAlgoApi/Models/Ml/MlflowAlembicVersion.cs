using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("alembic_version", Schema = "mlflow")]
public sealed class MlflowAlembicVersion
{
    [Key]
    [Column("version_num")]
    public string VersionNum { get; set; } = null!;
}
