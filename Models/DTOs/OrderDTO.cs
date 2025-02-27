using System;
using System.Collections.Generic;

namespace DoAnKy3.DTOs
{
    public class OrderDto
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public DateTime? OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Status { get; set; }
        public List<OrderDetailDto>? OrderDetails { get; set; }
        public List<PaymentDto>? Payments { get; set; }
    }
}
