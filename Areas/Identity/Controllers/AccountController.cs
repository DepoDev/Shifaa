using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shifaa.Services;
using System.Security.Claims;

namespace Shifaa.Areas.Identity.Controllers
{
    [Route("api/v1/auth")]
    [ApiController]
    [Area("Identity")]
    public class AccountController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AccountController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register/member")]
        public async Task<IActionResult> RegisterMember(MemberRegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.RegisterMemberAsync(request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message });
        }

        [HttpPost("register/caregiver")]
        public async Task<IActionResult> RegisterCaregiver(CaregiverRegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.RegisterCaregiverAsync(request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message });
        }

        [HttpPost("register/doctor")]
        public async Task<IActionResult> RegisterDoctor(DoctorRegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.RegisterDoctorAsync(request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message });
        }

        [HttpPost("register/medical-center")]
        public async Task<IActionResult> RegisterMedicalCenter(MedicalCenterRegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.RegisterMedicalCenterAsync(request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.LoginAsync(request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Data });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated." });

            var result = await _authService.LogoutAsync(userId);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message });
        }

        [HttpPost("switch-role")]
        [Authorize]
        public async Task<IActionResult> SwitchRole([FromBody] SwitchRoleRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated." });

            var result = await _authService.SwitchRoleAsync(userId, request.NewRole);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Data });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgetPasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.ForgotPasswordAsync(request.UserNameOrEmail);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message });
        }

        [HttpPost("validate-otp")]
        public async Task<IActionResult> ValidateOtp(ValidateOTPRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.ValidateOtpAsync(request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.ResetPasswordAsync(request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken(TokenApiRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.RefreshTokenAsync(request);
            return StatusCode(result.StatusCode, new { success = result.Success, message = result.Message, data = result.Data });
        }
        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
        {
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Token))
                return BadRequest(new { success = false, message = "UserId and Token are required." });

            var result = await _authService.ConfirmEmailAsync(request.UserId, request.Token);
            return StatusCode(result.StatusCode,
                new { success = result.Success, message = result.Message });
        }
    }
}
