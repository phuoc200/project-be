using DoAnKy3.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using DoAnKy3.Models.DTOs;

namespace DoAnKy3.Controllers
{
    [Route("api/admin")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly ClinicManagementSystemContext _context;

        public AdminController(ClinicManagementSystemContext context)
        {
            _context = context;
        }

        // API: Lấy danh sách tất cả tài khoản
        [HttpGet("get-all-users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.RoleId
                }).ToListAsync();

            return Ok(users);
        }

        // Cập nhật vai trò người dùng
        [HttpPut("update-role/{userId}")]
        public async Task<IActionResult> UpdateUserRole(int userId, [FromBody] int newRoleId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User không tồn tại." });
            }

            user.RoleId = newRoleId;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật Role thành công!" });
        }

        // Cập nhật thông tin tài khoản người dùng
        [HttpPut("update-user/{userId}")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody] AdminUpdateUserModel model)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User không tồn tại." });
            }

            // Kiểm tra username hoặc email đã tồn tại chưa
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => (u.Email == model.Email || u.Username == model.Username) && u.UserId != userId);
            if (existingUser != null)
            {
                return BadRequest(new { message = "Username hoặc email đã tồn tại." });
            }

            user.Username = model.Username;
            user.Email = model.Email;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật thông tin tài khoản thành công!" });
        }

        // Xóa tài khoản
        [HttpDelete("delete-user/{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User không tồn tại." });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa tài khoản thành công!" });
        }

        // Thêm sản phẩm
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
                Price = productDto.OriginalPrice - (productDto.OriginalPrice * productDto.Discount / 100), // Giá sau giảm
                Image = productDto.Image,
                Description = productDto.Description,
                CreatedAt = DateTime.Now,
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Thêm sản phẩm thành công!", product });
        }

        // Cập nhật sản phẩm
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductDTO productDto)
        {
            if (id != productDto.Id) return BadRequest("Product ID mismatch.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.Name = productDto.Name;
            product.Brand = productDto.Brand;
            product.Category = productDto.Category;
            product.OriginalPrice = productDto.OriginalPrice;
            product.Discount = productDto.Discount;
            product.Price = productDto.OriginalPrice - (productDto.OriginalPrice * productDto.Discount / 100); // Cập nhật giá
            product.Image = productDto.Image;
            product.Description = productDto.Description;

            _context.Entry(product).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật sản phẩm thành công!", product });
        }

        // Xóa sản phẩm
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa sản phẩm thành công!" });
        }

        public class AdminUpdateUserModel
        {
            [Required(ErrorMessage = "Username không được để trống.")]
            [StringLength(50, ErrorMessage = "Username không được vượt quá 50 ký tự.")]
            public string Username { get; set; }

            [Required(ErrorMessage = "Email không được để trống.")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
            public string Email { get; set; }

            // [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
            // public string FullName { get; set; }

            // [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
            // public string Phone { get; set; }

            // [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
            // public string Address { get; set; }

            // public DateTime? DateOfBirth { get; set; }

            // [Range(0, 1, ErrorMessage = "Giới tính không hợp lệ. (0: male, 1: female)")]
            // public int? Gender { get; set; }
        }
    }
}
