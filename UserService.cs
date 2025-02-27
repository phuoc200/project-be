using DoAnKy3.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnKy3
{
    public interface IUserService
    {
        Task<List<User>> GetUsersAsync();
    }

    public class UserService : IUserService
    {
        private readonly ClinicManagementSystemContext _context;

        public UserService(ClinicManagementSystemContext context)
        {
            _context = context;
        }

        public async Task<List<User>> GetUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }
    }

}
