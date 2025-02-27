using System;
using System.Collections.Generic;

namespace DoAnKy3.Models;

public partial class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Brand { get; set; }

    public string Category { get; set; } = null!;

    public decimal? OriginalPrice { get; set; }

    public decimal? Discount { get; set; }

    public decimal? Price { get; set; }

    public string Image { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
