namespace Morpheus.Database.Models;
public class User_Activity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

}
