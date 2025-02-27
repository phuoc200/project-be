namespace DoAnKy3.DTOs
{
    public class CartDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Image { get; set; }
        public int Quantity { get; set; } = 1; // Đặt giá trị mặc định là 1
        public DateTime AddedAt { get; set; } = DateTime.UtcNow; // Đặt giá trị mặc định
    }

}