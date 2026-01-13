using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerfectKeyV1.Domain.Enums;

namespace PerfectKeyV1.Application.DTOs.Auth
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserInfoDto User { get; set; } = new();
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class UserInfoDto
    {
        public int Id { get; set; }
        public Guid Guid { get; set; }
        public string UserName { get; set; } = string.Empty; // Đổi từ Username thành UserName
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string HotelCode { get; set; } = string.Empty;
        public string HotelName { get; set; } = string.Empty;
        public string? HotelAvatarUrl { get; set; }
        public UserStatus Status { get; set; } // Thêm property Status
        public UserType? UserType { get; set; } // Thêm property UserType
    }
}
