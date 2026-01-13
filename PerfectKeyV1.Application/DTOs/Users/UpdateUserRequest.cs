using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PerfectKeyV1.Domain.Enums;

namespace PerfectKeyV1.Application.DTOs.Users
{
    public class UpdateUserRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserType? UserType { get; set; }
        public UserStatus Status { get; set; }
        public string? Mobile { get; set; }
    }
}
