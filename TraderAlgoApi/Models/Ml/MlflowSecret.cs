using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraderAlgoApi.Models.Ml;

[Table("secrets", Schema = "mlflow")]
public sealed class MlflowSecret
{
    [Key]
    [Column("secret_id")]
    public string SecretId { get; set; } = null!;

    [Column("secret_name")]
    public string SecretName { get; set; } = null!;

    [Column("encrypted_value")]
    public byte[] EncryptedValue { get; set; } = null!;

    [Column("wrapped_dek")]
    public byte[] WrappedDek { get; set; } = null!;

    [Column("kek_version")]
    public int KekVersion { get; set; }

    [Column("masked_value")]
    public string MaskedValue { get; set; } = null!;

    [Column("provider")]
    public string? Provider { get; set; }

    [Column("auth_config")]
    public string? AuthConfig { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public long CreatedAt { get; set; }

    [Column("last_updated_by")]
    public string? LastUpdatedBy { get; set; }

    [Column("last_updated_at")]
    public long LastUpdatedAt { get; set; }

    [Column("workspace")]
    public string Workspace { get; set; } = null!;
}
