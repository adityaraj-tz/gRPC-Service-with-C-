namespace GrpcServiceProject.Models
{
    public class Book
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public int Pages { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
