using DoAnKy3.Models.DTOs;
using DoAnKy3.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace DoAnKy3.Controllers
{
    [Route("api/order")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly ClinicManagementSystemContext _context;
        private readonly IConfiguration _configuration;

        public OrderController(ClinicManagementSystemContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // Thanh toán và tạo đơn hàng
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] int userId)
        {
            var cartItems = await _context.Carts
                .Where(c => c.UserId == userId)
                .Include(c => c.Product)
                .ToListAsync();

            if (!cartItems.Any()) return BadRequest("Giỏ hàng trống!");

            decimal totalAmount = (decimal)cartItems.Sum(c => c.Quantity * c.Product.Price);

            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.UtcNow,
                TotalAmount = totalAmount,
                Status = "Pending"
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Tạo Payment với PayPal
            var payPalUrl = await CreatePayPalPayment(order.OrderId, totalAmount);

            return Ok(new { PaymentUrl = payPalUrl });
        }

        // ✅ Tạo thanh toán PayPal thực tế
        private async Task<string> CreatePayPalPayment(int orderId, decimal amount)
        {
            string clientId = _configuration["PayPal:ClientId"];
            string secret = _configuration["PayPal:Secret"];
            string mode = _configuration["PayPal:Mode"]; // "live" hoặc "sandbox"

            string baseUrl = mode == "live"
                ? "https://api-m.paypal.com"
                : "https://api-m.sandbox.paypal.com";

            using (var client = new HttpClient())
            {
                // Lấy Access Token
                var authToken = Encoding.ASCII.GetBytes($"{clientId}:{secret}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                var tokenResponse = await client.PostAsync($"{baseUrl}/v1/oauth2/token",
                new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded"));

                if (!tokenResponse.IsSuccessStatusCode)
                    return "Lỗi khi lấy Access Token từ PayPal!";

                var tokenResult = JsonConvert.DeserializeObject<dynamic>(await tokenResponse.Content.ReadAsStringAsync());
                if (tokenResult == null || tokenResult.access_token == null)
                    return "Lỗi: Không nhận được Access Token từ PayPal.";

                string accessToken = tokenResult.access_token;


                // Tạo đơn hàng
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var orderData = new
                {
                    intent = "CAPTURE",
                    purchase_units = new[]
                    {
                new
                {
                    amount = new
                    {
                        currency_code = "USD",
                        value = amount.ToString("F2")
                    }
                }
            },
                    application_context = new
                    {
                        return_url = "http://localhost:5000/api/payment/success",
                        cancel_url = "http://localhost:5000/api/payment/cancel"
                    }
                };

                var orderContent = new StringContent(JsonConvert.SerializeObject(orderData), Encoding.UTF8, "application/json");
                var orderResponse = await client.PostAsync($"{baseUrl}/v2/checkout/orders", orderContent);
                var orderResult = JsonConvert.DeserializeObject<dynamic>(await orderResponse.Content.ReadAsStringAsync());

                return orderResult.links[1].href; // Trả về link thanh toán PayPal
            }
        }

        // Hàm xác nhận từ PayPal
        private async Task<string?> VerifyPayPalPayment(string token)
        {
            try
            {
                string clientId = _configuration["PayPal:ClientId"];
                string secret = _configuration["PayPal:Secret"];
                string mode = _configuration["PayPal:Mode"];

                string baseUrl = mode == "live"
                    ? "https://api-m.paypal.com"
                    : "https://api-m.sandbox.paypal.com";

                using (var client = new HttpClient())
                {
                    // Lấy Access Token
                    var authToken = Encoding.ASCII.GetBytes($"{clientId}:{secret}");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                    var tokenResponse = await client.PostAsync($"{baseUrl}/v1/oauth2/token",
                        new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded"));

                    if (!tokenResponse.IsSuccessStatusCode)
                        return null;

                    var tokenResult = JsonConvert.DeserializeObject<dynamic>(await tokenResponse.Content.ReadAsStringAsync());
                    string accessToken = tokenResult.access_token;

                    // Kiểm tra trạng thái thanh toán bằng token
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var orderResponse = await client.GetAsync($"{baseUrl}/v2/checkout/orders/{token}");

                    if (!orderResponse.IsSuccessStatusCode)
                        return null;

                    var orderData = JsonConvert.DeserializeObject<dynamic>(await orderResponse.Content.ReadAsStringAsync());

                    // In log để debug
                    Console.WriteLine("PayPal Response: " + orderData.ToString());

                    // Kiểm tra trạng thái giao dịch
                    if (orderData.status == "COMPLETED")
                    {
                        return orderData.id; // Trả về TransactionId
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi xác nhận thanh toán từ PayPal: " + ex.Message);
                return null;
            }
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetOrders(int userId)
        {
            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderDetails)  // Assuming there's a relationship with OrderItems (which represents products in the order)
                .ThenInclude(oi => oi.Product)  // Including the product details in each order item
                .Include(o => o.User)  // To get the customer name
                .Select(o => new
                {
                    Products = o.OrderDetails.Select(oi => new
                    {
                        oi.Product.Name,  // Assuming there's a 'Name' field in the Product model
                        oi.Quantity,
                    }),
                    o.OrderId,
                    o.OrderDate,
                    CustomerName = o.User.Username,  // Assuming 'U' is the customer's name
                    o.TotalAmount,
                    o.Status,
                })
                .ToListAsync();

            return Ok(orders);
        }


    }
}
