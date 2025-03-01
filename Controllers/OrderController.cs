using DoAnKy3.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using DoAnKy3.Models.DTOs; // Add this line
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

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

        // API: Lấy danh sách đơn hàng theo userId
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetOrders(int userId)
        {
            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderDate,
                    o.TotalAmount,
                    o.Status,
                    // Get customer name from Users table
                    CustomerName = _context.Users
                        .Where(u => u.UserId == o.UserId)
                        .Select(u => u.Username)
                        .FirstOrDefault() ?? "Unknown",
                    // Get products from OrderDetails table (if you have one)
                    Products = _context.OrderDetails
                        .Where(od => od.OrderId == o.OrderId)
                        .Select(od => new
                        {
                            Name = _context.Products
                                .Where(p => p.Id == od.ProductId)
                                .Select(p => p.Name)
                                .FirstOrDefault() ?? "Unknown Product",
                            od.Quantity
                        })
                        .ToList(),
                    Payment = _context.Payments
                        .Where(p => p.OrderId == o.OrderId)
                        .Select(p => new
                        {
                            p.PaymentMethod,
                            p.PaymentStatus,
                            p.PaymentDate,
                            p.Amount
                        })
                        .FirstOrDefault()
                })
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return Ok(orders);
        }

        // API: Tạo đơn hàng và thanh toán
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // 1. Retrieve cart items for the user
                var cartItems = await _context.Carts
                    .Where(c => c.UserId == request.UserId)
                    .Include(c => c.Product)
                    .ToListAsync();

                if (!cartItems.Any())
                    return BadRequest("Giỏ hàng trống!");

                // 2. Calculate total amount
                decimal totalAmount = cartItems.Sum(c => c.Quantity * c.Price);


                // 3. Create order
                var order = new Order
                {
                    UserId = request.UserId,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = totalAmount,
                    Status = "Completed",
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // 3.1. Create order details
                foreach (var cartItem in cartItems)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.OrderId,
                        ProductId = cartItem.ProductId,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.Price,
                    };
                    _context.OrderDetails.Add(orderDetail);
                }
                await _context.SaveChangesAsync();

                // 4. Create Payment with PayPal
                var payPalUrl = await CreatePayPalPayment(order.OrderId, totalAmount);
                return Ok(new { PaymentUrl = payPalUrl });
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error in Checkout: {ex}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private async Task<string> CreatePayPalPayment(int orderId, decimal amount)
        {
            try
            {
                string clientId = _configuration["PayPal:ClientId"];
                string secret = _configuration["PayPal:Secret"];
                string mode = _configuration["PayPal:Mode"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret))
                {
                    return "Lỗi: Thiếu thông tin cấu hình PayPal!";
                }

                string baseUrl = mode == "live"
                    ? "https://api-m.paypal.com"
                    : "https://api-m.sandbox.paypal.com";

                using (var client = new HttpClient())
                {
                    // Lấy Access Token
                    var authToken = Encoding.ASCII.GetBytes($"{clientId}:{secret}");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                    var tokenContent = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");
                    var tokenResponse = await client.PostAsync($"{baseUrl}/v1/oauth2/token", tokenContent);

                    if (!tokenResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                        return $"Lỗi khi lấy Access Token từ PayPal! Status: {tokenResponse.StatusCode}, Error: {errorContent}";
                    }

                    var tokenResult = JObject.Parse(await tokenResponse.Content.ReadAsStringAsync());
                    string accessToken = tokenResult["access_token"].ToString();

                    // Tạo đơn hàng PayPal
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var orderData = new
                    {
                        intent = "CAPTURE",
                        purchase_units = new[]
                        {
                            new
                            {
                                reference_id = orderId.ToString(),
                                description = "Payment for Order #" + orderId,
                                amount = new
                                {
                                    currency_code = "USD",
                                    value = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                                }
                            }
                        },
                        application_context = new
                        {
                            brand_name = "Your Store Name",
                            landing_page = "NO_PREFERENCE",
                            user_action = "PAY_NOW",
                            return_url = "http://localhost:5000/api/order/payment/success",
                            cancel_url = "http://localhost:5000/api/order/payment/cancel"
                        }
                    };

                    var orderContent = new StringContent(JsonConvert.SerializeObject(orderData), Encoding.UTF8, "application/json");
                    var orderResponse = await client.PostAsync($"{baseUrl}/v2/checkout/orders", orderContent);

                    if (!orderResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await orderResponse.Content.ReadAsStringAsync();
                        return $"Lỗi khi tạo đơn hàng PayPal: {orderResponse.StatusCode}";
                    }

                    var orderResult = JObject.Parse(await orderResponse.Content.ReadAsStringAsync());
                    var links = orderResult["links"] as JArray;
                    var approveLink = links?.FirstOrDefault(x => x["rel"].ToString() == "approve");

                    return approveLink?["href"]?.ToString() ?? "Không tìm thấy link thanh toán";
                }
            }
            catch (Exception ex)
            {
                return $"Lỗi hệ thống: {ex.Message}";
            }
        }

        [HttpGet("payment/success")]
        public async Task<IActionResult> PaymentSuccess([FromQuery] string token)
        {
            try
            {
                var paypalResponse = await VerifyPayPalPayment(token);
                if (paypalResponse == null)
                {
                    return BadRequest("Không thể xác thực thanh toán từ PayPal");
                }

                var orderData = JObject.Parse(paypalResponse);
                var orderId = int.Parse(orderData["purchase_units"][0]["reference_id"].ToString());
                var amount = decimal.Parse(orderData["purchase_units"][0]["amount"]["value"].ToString());

                var payment = new Payment
                {
                    OrderId = orderId,
                    PaymentMethod = "E-Wallet",
                    PaymentStatus = "Completed",
                    PaymentDate = DateTime.UtcNow,
                    Amount = amount
                };

                var order = await _context.Orders.FindAsync(orderId);
                if (order != null)
                {
                    order.Status = "Completed";
                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    // Xóa giỏ hàng
                    var cartItems = await _context.Carts
                        .Where(c => c.UserId == order.UserId)
                        .ToListAsync();
                    if (cartItems.Any())
                    {
                        _context.Carts.RemoveRange(cartItems);
                        await _context.SaveChangesAsync();
                    }

                    return Redirect("http://localhost:3000/checkout/payment-success");
                }

                return BadRequest("Không tìm thấy đơn hàng");
            }
            catch (Exception ex)
            {
                return BadRequest($"Lỗi xử lý thanh toán: {ex.Message}");
            }
        }

        [HttpGet("payment/cancel")]
        public async Task<IActionResult> PaymentCancel([FromQuery] string token)
        {
            try
            {
                var orderData = await VerifyPayPalPayment(token);
                if (orderData != null)
                {
                    var jObject = JObject.Parse(orderData);
                    var orderId = int.Parse(jObject["purchase_units"][0]["reference_id"].ToString());

                    var order = await _context.Orders.FindAsync(orderId);
                    if (order != null)
                    {
                        order.Status = "Cancelled";
                        await _context.SaveChangesAsync();
                    }
                }

                return Redirect("http://localhost:3000/payment-cancel");
            }
            catch
            {
                return Redirect("http://localhost:3000/payment-cancel");
            }
        }

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
                    var authToken = Encoding.ASCII.GetBytes($"{clientId}:{secret}");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                    var tokenResponse = await client.PostAsync($"{baseUrl}/v1/oauth2/token",
                        new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded"));

                    if (!tokenResponse.IsSuccessStatusCode)
                        return null;

                    var tokenResult = JObject.Parse(await tokenResponse.Content.ReadAsStringAsync());
                    string accessToken = tokenResult["access_token"].ToString();

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var orderResponse = await client.GetAsync($"{baseUrl}/v2/checkout/orders/{token}");

                    if (!orderResponse.IsSuccessStatusCode)
                        return null;

                    var responseContent = await orderResponse.Content.ReadAsStringAsync();
                    var orderData = JObject.Parse(responseContent);

                    if (orderData["status"].ToString() == "COMPLETED" ||
                        orderData["status"].ToString() == "APPROVED")
                    {
                        return responseContent;
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi xác thực PayPal: {ex.Message}");
                return null;
            }
        }
    }
}