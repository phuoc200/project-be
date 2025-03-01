namespace DoAnKy3.Models.DTOs
{
    public class CheckoutRequest
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address { get; set; }
        public string Apartment { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Phone { get; set; }
        public string PaymentMethod { get; set; }
        // You may not need Items here.
        // public List<CartItemDTO> Items { get; set; }  // If you need to pass cart items info here
    }
}