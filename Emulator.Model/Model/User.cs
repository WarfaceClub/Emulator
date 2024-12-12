using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Emulator.Model;

[Table("wf_user")]
[Index(nameof(Login), Name = "idx_user_login")]
public class User
{
    [Key, Column("id"), DatabaseGenerated(DatabaseGeneratedOption.Identity), Required]
    public uint Id { get; set; }

    [Key, Column("login"), Required]
    public string Login { get; set; }

    [Column("password"), Required]
    public string Password { get; set; }

    [Column("discord_user_id")]
    public ulong? DiscordUserId { get; set; }

    [Column("created_at"), DatabaseGenerated(DatabaseGeneratedOption.Identity), Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at"), DatabaseGenerated(DatabaseGeneratedOption.Computed), Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
