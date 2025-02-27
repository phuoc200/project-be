using Microsoft.AspNetCore.Mvc;
using DoAnKy3.Models;
using DoAnKy3.Models.DTOs;
using System.Linq;
using System;
using Microsoft.EntityFrameworkCore;
using DoAnKy3.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace DoAnKy3.Controllers
{
    [Route("api/cart")]
    [ApiController]
    [Authorize] // Áp dụng xác thực cho toàn bộ controller
    public class CartController : ControllerBase
    {
        private readonly ClinicManagementSystemContext _context;

        public CartController(ClinicManagementSystemContext context)
        {
            _context = context;
        }

        // Lấy userId từ token JWT
        private int GetUserIdFromToken()
        {
            var identity = HttpContext.User.Identity as ClaimsIdentity;
            if (identity != null)
            {
                var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    return userId;
                }
            }
            return 0;
        }

        // Thêm sản phẩm vào giỏ hàng
        [HttpPost("add")]
        public async Task<IActionResult> AddToCart([FromBody] CartDto cartDto)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (cartDto == null || cartDto.ProductId <= 0)
                {
                    return BadRequest(new { message = "Dữ liệu không hợp lệ!" });
                }

                // Lấy userId từ token thay vì từ request
                int userId = GetUserIdFromToken();
                if (userId == 0)
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng!" });
                }

                // Gán userId từ token vào cartDto
                cartDto.UserId = userId;

                // Đặt số lượng mặc định nếu không được cung cấp
                if (cartDto.Quantity <= 0)
                {
                    cartDto.Quantity = 1;
                }

                var product = await _context.Products.FindAsync(cartDto.ProductId);
                if (product == null)
                    return NotFound(new { message = "Sản phẩm không tồn tại!" });

                // Kiểm tra sản phẩm đã có trong giỏ hàng của người dùng chưa
                var existingCartItem = await _context.Carts
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == cartDto.ProductId);

                if (existingCartItem != null)
                {
                    existingCartItem.Quantity += cartDto.Quantity;
                }
                else
                {
                    // Xử lý trường hợp product.Price là null
                    decimal price = cartDto.Price;

                    // Nếu giá từ frontend là 0, thì mới sử dụng giá từ database
                    if (price == 0 && product.Price.HasValue)
                    {
                        price = product.Price.Value;
                    }

                    var cart = new Cart
                    {
                        UserId = userId,
                        ProductId = cartDto.ProductId,
                        Name = product.Name ?? "Unknown Product", // Xử lý trường hợp Name là null
                        Price = price, // Sử dụng giá trị đã kiểm tra null
                        Image = product.Image ?? "", // Xử lý trường hợp Image là null
                        Quantity = cartDto.Quantity,
                        AddedAt = DateTime.UtcNow
                    };
                    _context.Carts.Add(cart);
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Thêm vào giỏ hàng thành công!" });
            }
            catch (Exception ex)
            {
                // Log lỗi để debug
                Console.WriteLine($"Error in AddToCart: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
            }
        }

        // Xem giỏ hàng theo UserId
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetCart(int userId)
        {
            try
            {
                // Lấy userId từ token để kiểm tra quyền truy cập
                int tokenUserId = GetUserIdFromToken();
                if (tokenUserId == 0)
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng!" });
                }

                // Chỉ cho phép người dùng xem giỏ hàng của chính họ
                if (tokenUserId != userId)
                {
                    return Forbid();
                }

                var cartItems = await _context.Carts
                    .Where(c => c.UserId == userId)
                    .Select(c => new CartDto
                    {
                        Id = c.Id,
                        UserId = c.UserId,
                        ProductId = c.ProductId,
                        Name = c.Name ?? "Unknown Product",
                        Price = c.Price,
                        Image = c.Image ?? "",
                        Quantity = c.Quantity,
                        AddedAt = c.AddedAt ?? DateTime.UtcNow
                    })
                    .ToListAsync();

                // Trả về danh sách trống thay vì NotFound khi giỏ hàng trống
                return Ok(cartItems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCart: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
            }
        }

        // Xóa sản phẩm khỏi giỏ hàng
        [HttpDelete("remove/{cartId}")]
        public async Task<IActionResult> RemoveFromCart(int cartId)
        {
            try
            {
                // Lấy userId từ token để kiểm tra quyền truy cập
                int userId = GetUserIdFromToken();
                if (userId == 0)
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng!" });
                }

                var cartItem = await _context.Carts.FindAsync(cartId);
                if (cartItem == null) return NotFound(new { message = "Sản phẩm không tồn tại trong giỏ hàng!" });

                // Chỉ cho phép người dùng xóa sản phẩm trong giỏ hàng của chính họ
                if (cartItem.UserId != userId)
                {
                    return Forbid();
                }

                _context.Carts.Remove(cartItem);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Xóa sản phẩm khỏi giỏ hàng thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RemoveFromCart: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
            }
        }

        // Cập nhật số lượng sản phẩm trong giỏ hàng
        [HttpPut("update/{cartId}")]
        public async Task<IActionResult> UpdateCartItem(int cartId, [FromBody] UpdateCartItemDto updateDto)
        {
            try
            {
                // Lấy userId từ token để kiểm tra quyền truy cập
                int userId = GetUserIdFromToken();
                if (userId == 0)
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng!" });
                }

                if (updateDto == null || updateDto.Quantity <= 0)
                {
                    return BadRequest(new { message = "Số lượng không hợp lệ!" });
                }

                var cartItem = await _context.Carts.FindAsync(cartId);
                if (cartItem == null) return NotFound(new { message = "Sản phẩm không tồn tại trong giỏ hàng!" });

                // Chỉ cho phép người dùng cập nhật sản phẩm trong giỏ hàng của chính họ
                if (cartItem.UserId != userId)
                {
                    return Forbid();
                }

                cartItem.Quantity = updateDto.Quantity;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Cập nhật số lượng thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateCartItem: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
            }
        }

        // Xóa tất cả sản phẩm trong giỏ hàng của một người dùng
        [HttpDelete("clear/{userId}")]
        public async Task<IActionResult> ClearCart(int userId)
        {
            try
            {
                // Lấy userId từ token để kiểm tra quyền truy cập
                int tokenUserId = GetUserIdFromToken();
                if (tokenUserId == 0)
                {
                    return Unauthorized(new { message = "Không thể xác thực người dùng!" });
                }

                // Chỉ cho phép người dùng xóa giỏ hàng của chính họ
                if (tokenUserId != userId)
                {
                    return Forbid();
                }

                var cartItems = await _context.Carts.Where(c => c.UserId == userId).ToListAsync();
                if (!cartItems.Any()) return Ok(new { message = "Giỏ hàng đã trống!" });

                _context.Carts.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Đã xóa tất cả sản phẩm trong giỏ hàng!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ClearCart: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
            }
        }
    }

    public class UpdateCartItemDto
    {
        public int Quantity { get; set; }
    }
}