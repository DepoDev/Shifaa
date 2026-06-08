using System.ComponentModel.DataAnnotations;
using Shifaa.Utilities;
using static Shifaa.Models.Enums;

namespace Shifaa.DTOs.Request
{
    public class LoginRequest
    {
        public UserType Role { get; set; }
        public string UserNameOrEmail { get; set; } = string.Empty;
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
}
