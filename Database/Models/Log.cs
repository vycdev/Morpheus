using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;
public class Log
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    
    public int Severity { get; set; }
    public string Message { get; set; }

    public string Version { get; set; }

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;
}
