using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;
public class Log
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    public int Severity { get; set; }
    public string Message { get; set; }

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;
}
