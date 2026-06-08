using static Shifaa.Models.Enums;

namespace Shifaa.Services
{
    public interface IAuthService
    {
        Task<ServiceResult> RegisterMemberAsync(MemberRegisterRequest request);
        Task<ServiceResult> RegisterCaregiverAsync(CaregiverRegisterRequest request);
        Task<ServiceResult> RegisterDoctorAsync(DoctorRegisterRequest request);
        Task<ServiceResult> RegisterMedicalCenterAsync(MedicalCenterRegisterRequest request);
        Task<ServiceResult<AuthenticatedResponse>> LoginAsync(LoginRequest request);
        Task<ServiceResult<AuthenticatedResponse>> LogoutAsync();
        Task<ServiceResult<AuthenticatedResponse>> SwitchRoleAsync(string userId, UserType newRole);
        Task<ServiceResult> ForgotPasswordAsync(string email);
        Task<ServiceResult> ValidateOtpAsync(ValidateOTPRequest request);
        Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request);
        Task<ServiceResult<AuthenticatedResponse>> RefreshTokenAsync(TokenApiRequest request);
        Task<ServiceResult> ConfirmEmailAsync(string userId, string token);
    }

    // Generic result wrapper — keeps controller responses consistent
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int StatusCode { get; set; }

        public static ServiceResult Ok(string message = "Success") =>
            new() { Success = true, Message = message, StatusCode = 200 };

        public static ServiceResult Fail(string message, int statusCode = 400) =>
            new() { Success = false, Message = message, StatusCode = statusCode };
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; set; }

        public static ServiceResult<T> Ok(T data, string message = "Success") =>
            new() { Success = true, Message = message, StatusCode = 200, Data = data };

        public static new ServiceResult<T> Fail(string message, int statusCode = 400) =>
            new() { Success = false, Message = message, StatusCode = statusCode };
    }
}
