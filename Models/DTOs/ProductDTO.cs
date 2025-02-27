using System.ComponentModel.DataAnnotations;

namespace DoAnKy3.Models.DTOs
{
    public class ProductDTO
    {
        public int Id { get; set; } // Khớp với cột Id trong bảng

        [Required]
        public string Name { get; set; } = null!;

        public string? Brand { get; set; }

        [Required]
        public string Category { get; set; } = null!; // Giá trị ENUM giả lập ('lte', 'scientific', 'medical')

        public decimal? OriginalPrice { get; set; } // Giá gốc có thể NULL

        public decimal? Discount { get; set; } // Chiết khấu có thể NULL

        public decimal Price { get; set; } // Giá sau giảm giá (Computed Column trong DB)

        [Required]
        public string Image { get; set; } = null!;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } // Mặc định lấy từ DB (GETDATE())
    }
}
