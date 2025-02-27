using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DoAnKy3.Controllers
{
    [Route("api/payment")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly ILogger<PaymentController> _logger;
        private readonly HttpClient _httpClient;

        public PaymentController(ILogger<PaymentController> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        // 1️⃣ API xác nhận thanh toán
        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            try
            {
                // ✅ Gửi yêu cầu xác nhận tới cổng thanh toán (VD: PayPal, VNPay, MoMo...)
                string paymentGatewayUrl = $"https://api.sandbox.paypal.com/v1/payments/payment/{request.PaymentId}/execute";

                var content = new StringContent(JsonSerializer.Serialize(new { payer_id = request.PayerID }), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(paymentGatewayUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Xác nhận thanh toán thất bại: {response.StatusCode}");
                    return BadRequest(new { message = "Xác nhận thanh toán thất bại." });
                }

                // ✅ Đọc phản hồi từ cổng thanh toán
                var responseData = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Thanh toán được xác nhận thành công: " + responseData);

                return Ok(new { message = "Thanh toán đã được xác nhận", data = responseData });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi xác nhận thanh toán: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi nội bộ khi xác nhận thanh toán" });
            }
        }

        // 2️⃣ API xử lý thành công thanh toán
        [HttpGet("success")]
        public IActionResult Success([FromQuery] string token, [FromQuery] string PayerID)
        {
            return Ok(new { message = "Payment Successful", token, PayerID });
        }

        // 3️⃣ API xử lý hủy thanh toán
        [HttpGet("cancel")]
        public IActionResult Cancel()
        {
            return Ok(new { message = "Payment Cancelled" });
        }
    }

    // ✅ Model request để xác nhận thanh toán
    public class ConfirmPaymentRequest
    {
        public string PaymentId { get; set; }
        public string PayerID { get; set; }
    }
}
