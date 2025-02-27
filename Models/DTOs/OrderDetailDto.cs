namespace DoAnKy3.DTOs
{
    public class OrderDetailDto
    {
        public int OrderDetailId { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // Optional: Thêm thông tin sản phẩm nếu cần
        public string? ProductName { get; set; }
    }
}
