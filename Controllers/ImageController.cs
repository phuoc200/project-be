using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using DoAnKy3.Models;

[Route("api/[controller]")]
[ApiController]
public class ImageController : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine("wwwroot/images", fileName);

        if (!Directory.Exists("wwwroot/images"))
            Directory.CreateDirectory("wwwroot/images");

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var imageUrl = $"/images/{fileName}";
        return Ok(new { imageUrl });
    }
}
