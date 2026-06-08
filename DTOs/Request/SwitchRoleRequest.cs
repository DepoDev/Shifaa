using System.ComponentModel.DataAnnotations;
using static Shifaa.Models.Enums;

namespace Shifaa.DTOs.Request
{
    public class SwitchRoleRequest
    {
        [Required(ErrorMessage = "Role is required")]
        public UserType NewRole { get; set; }
    }
}
