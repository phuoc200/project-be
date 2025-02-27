using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnKy3.Models;
using DoAnKy3.Models.DTOs;
using System.Threading.Tasks;

namespace DoAnKy3.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly ClinicManagementSystemContext _context;

        public ProductController(ClinicManagementSystemContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> AddProduct([FromBody] ProductDTO productDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var product = new Product
            {
                Name = productDto.Name,
                Brand = productDto.Brand,
                Category = productDto.Category,
                OriginalPrice = productDto.OriginalPrice,
                Discount = productDto.Discount,
                Price = productDto.OriginalPrice - (productDto.OriginalPrice * productDto.Discount / 100), // Tính giá sau giảm
                Image = productDto.Image,
                Description = productDto.Description,
                CreatedAt = DateTime.Now,
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Thêm sản phẩm thành công!", product });
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductDTO productDto)
        {
            if (id != productDto.Id) return BadRequest("Product ID mismatch.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null) return NotFound("Product not found.");

                product.Name = productDto.Name;
                product.Brand = productDto.Brand;
                product.Category = productDto.Category;
                product.OriginalPrice = productDto.OriginalPrice;
                product.Discount = productDto.Discount;
                product.Price = productDto.OriginalPrice - (productDto.OriginalPrice * productDto.Discount / 100);
                product.Image = productDto.Image;
                product.Description = productDto.Description;

                _context.Entry(product).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Cập nhật sản phẩm thành công!", product });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi cập nhật sản phẩm.", error = ex.Message });
            }
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa sản phẩm thành công!" });
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            return Ok(product);
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts(int pageNumber = 1, int pageSize = 6)
        {
            if (pageNumber < 1 || pageSize < 1) return BadRequest("Page number và page size phải lớn hơn 0.");

            var totalProducts = await _context.Products.CountAsync();
            var products = await _context.Products
                                         .OrderByDescending(p => p.CreatedAt) // Sắp xếp sản phẩm mới nhất
                                         .Skip((pageNumber - 1) * pageSize)
                                         .Take(pageSize)
                                         .ToListAsync();

            var result = new
            {
                TotalProducts = totalProducts,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize),
                Products = products
            };

            return Ok(result);
        }


    }


}