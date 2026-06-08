using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Shifaa.JwtFeatures;
using Shifaa.Repos;
using static Shifaa.Models.Enums;

namespace Shifaa.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IJwtHandler _jwtHandler;
        private readonly IEmailSender _emailSender;
        private readonly IRepository<ApplicationUserOTP> _otpRepository;
        private readonly IRepository<Member> _memberRepository;
        private readonly IRepository<Caregiver> _caregiverRepository;
        private readonly IRepository<Doctor> _doctorRepository;
        private readonly IRepository<MedicalCenter> _medicalCenterRepository;
        private readonly ILogger<AuthService> _logger;
        private readonly IFileService _fileService;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            IJwtHandler jwtHandler,
            IEmailSender emailSender,
            IRepository<ApplicationUserOTP> otpRepository,
            IRepository<Member> memberRepository,
            IRepository<Caregiver> caregiverRepository,
            IRepository<Doctor> doctorRepository,
            IRepository<MedicalCenter> medicalCenterRepository,
            ILogger<AuthService> logger,
            IFileService fileService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _jwtHandler = jwtHandler;
            _emailSender = emailSender;
            _otpRepository = otpRepository;
            _memberRepository = memberRepository;
            _caregiverRepository = caregiverRepository;
            _doctorRepository = doctorRepository;
            _medicalCenterRepository = medicalCenterRepository;
            _logger = logger;
            _fileService = fileService;
        }
        

        // ── MEMBER ───────────────────────────────────────────────────────
        public async Task<ServiceResult> RegisterMemberAsync(MemberRegisterRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingUser = await _userManager.FindByEmailAsync(request.Email);

                ApplicationUser user;
                if (existingUser is not null && !existingUser.IsDeleted)
                {
                    // User already exists — just add them to member role and create profile
                    user = existingUser;

                    // Only add role if not already in it
                    if (!await _userManager.IsInRoleAsync(user, SD.MEMBER_ROLE))
                    {
                        await _userManager.AddToRoleAsync(user, SD.MEMBER_ROLE);
                    }

                    // Check if member profile already exists
                    var existingMember = await _memberRepository
                        .GetOneAsync(m => m.UserId == user.Id);

                    if (existingMember is null)
                    {
                        await _memberRepository.AddAsync(new Member 
                        { 
                            UserId = user.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    // New user — create in AspNetUsers and add profile
                    user = BuildApplicationUser(request, UserType.Member);
                    var result = await _userManager.CreateAsync(user, request.Password);
                    if (!result.Succeeded)
                        return ServiceResult.Fail(
                            string.Join(", ", result.Errors.Select(e => e.Description)));

                    await _userManager.AddToRoleAsync(user, SD.MEMBER_ROLE);

                    // Create empty profile — member fills it after first login
                    await _memberRepository.AddAsync(new Member 
                    { 
                        UserId = user.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await SendEmailConfirmationAsync(user);

                return ServiceResult.Ok(
                    "Registration successful. Please confirm your email to continue.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering member: {Message}", ex.Message);
                await transaction.RollbackAsync();
                return ServiceResult.Fail($"Registration failed: {ex.Message}", 500);
            }
        }

        // ── CAREGIVER ─────────────────────────────────────────────────────
        public async Task<ServiceResult> RegisterCaregiverAsync(CaregiverRegisterRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingUser = await _userManager.FindByEmailAsync(request.Email);

                ApplicationUser user;
                if (existingUser is not null && !existingUser.IsDeleted)
                {
                    // User already exists — just add them to caregiver role and create profile
                    user = existingUser;

                    // Only add role if not already in it
                    if (!await _userManager.IsInRoleAsync(user, "Caregiver"))
                    {
                        await _userManager.AddToRoleAsync(user, "Caregiver");
                    }

                    // Check if caregiver profile already exists
                    var existingCaregiver = await _caregiverRepository
                        .GetOneAsync(c => c.UserId == user.Id);

                    if (existingCaregiver is null)
                    {
                        await _caregiverRepository.AddAsync(new Caregiver 
                        { 
                            UserId = user.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    // New user — create in AspNetUsers and add profile
                    user = BuildApplicationUser(request, UserType.Caregiver);
                    var result = await _userManager.CreateAsync(user, request.Password);
                    if (!result.Succeeded)
                        return ServiceResult.Fail(
                            string.Join(", ", result.Errors.Select(e => e.Description)));

                    await _userManager.AddToRoleAsync(user, "Caregiver");

                    // Create empty profile — caregiver fills it after first login
                    // RelationshipType is NOT here — it's per GuardianMember link
                    await _caregiverRepository.AddAsync(new Caregiver 
                    { 
                        UserId = user.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await SendEmailConfirmationAsync(user);

                return ServiceResult.Ok(
                    "Registration successful. Please confirm your email to continue.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering caregiver: {Message}", ex.Message);
                await transaction.RollbackAsync();
                return ServiceResult.Fail($"Registration failed: {ex.Message}", 500);
            }
        }

        // ── DOCTOR ────────────────────────────────────────────────────────
        // Account inactive until Admin approves
        // ConsultationFee NOT set here — Medical Center sets it during assignment
        public async Task<ServiceResult> RegisterDoctorAsync(DoctorRegisterRequest request)
        {
            var syndicateExists = await _context.Doctors
                                 .AnyAsync(d => d.MedicalSyndicateId == request.MedicalSyndicateId);
            if (syndicateExists)
                return ServiceResult.Fail(
                    "A doctor with this Medical Syndicate ID is already registered.", 400);

            var nationalIdExists = await _context.Doctors
                .AnyAsync(d => d.NationalId == request.NationalId);
            if (nationalIdExists)
                return ServiceResult.Fail(
                    "A doctor with this National ID is already registered.", 400);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingUser = await _userManager.FindByEmailAsync(request.Email);

                ApplicationUser user;
                if (existingUser is not null && !existingUser.IsDeleted)
                {
                    // User already exists — just add them to doctor role and create profile
                    user = existingUser;
                    user.PhoneNumber = request.PhoneNumber;

                    // Only add role if not already in it
                    if (!await _userManager.IsInRoleAsync(user, "Doctor"))
                    {
                        await _userManager.AddToRoleAsync(user, "Doctor");
                    }

                    // Check if doctor profile already exists
                    var existingDoctor = await _doctorRepository
                        .GetOneAsync(d => d.UserId == user.Id);

                    if (existingDoctor is null)
                    {
                        await _doctorRepository.AddAsync(new Doctor
                        {
                            UserId = user.Id,
                            Specialty = request.Specialty,
                            YearsOfExperience = request.YearsOfExperience,
                            Bio = request.Bio,
                            IsAvailable = false,   // Admin activates on approval
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    // Update existing user if needed
                    await _userManager.UpdateAsync(user);
                }
                else
                {
                    // New user — create in AspNetUsers and add profile
                    user = BuildApplicationUser(request, UserType.Doctor);
                    user.PhoneNumber = request.PhoneNumber;
                    user.EmailConfirmed = false;  // inactive until Admin approves

                    var result = await _userManager.CreateAsync(user, request.Password);
                    if (!result.Succeeded)
                        return ServiceResult.Fail(
                            string.Join(", ", result.Errors.Select(e => e.Description)));

                    await _userManager.AddToRoleAsync(user, "Doctor");

                    var folderPath = $"Uploads/DoctorVerification/{user.Id}";
                    var syndicateCardFile = await _fileService
                        .SaveFileAsync(request.SyndicateCardImage, folderPath);
                    var degreeFile = await _fileService
                        .SaveFileAsync(request.MedicalDegreeCertificate, folderPath);

                    await _doctorRepository.AddAsync(new Doctor
                    {
                        UserId = user.Id,
                        MedicalSyndicateId = request.MedicalSyndicateId,
                        NationalId = request.NationalId,
                        SyndicateCardImageFile = syndicateCardFile,
                        MedicalDegreeCertificateFile = degreeFile,
                        Specialty = request.Specialty,
                        YearsOfExperience = request.YearsOfExperience,
                        Bio = request.Bio,
                        IsAvailable = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Notify Admin to review
                await _emailSender.SendEmailAsync(
                    "admin@shifaa.com",
                    "New Doctor Registration — Pending Approval",
                    $"<h2>Doctor registration request</h2>" +
                    $"<p><b>Name:</b> {user.FirstName} {user.LastName}</p>" +
                    $"<p><b>Specialty:</b> {request.Specialty}</p>" +
                    $"<p><b>Email:</b> {user.Email}</p>" +
                    $"<p><b>Experience:</b> {request.YearsOfExperience} years</p>" +
                    $"<p>Please review and approve from the Admin dashboard.</p>");

                return ServiceResult.Ok(
                    "Your registration request has been submitted and is pending Admin review. " +
                    "You will receive an email once approved.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering doctor: {Message}", ex.Message);
                await transaction.RollbackAsync();
                return ServiceResult.Fail($"Registration failed: {ex.Message}", 500);
            }
        }

        // ── MEDICAL CENTER ────────────────────────────────────────────────
        // Same approval flow as Doctor
        public async Task<ServiceResult> RegisterMedicalCenterAsync(
            MedicalCenterRegisterRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingUser = await _userManager.FindByEmailAsync(request.Email);

                ApplicationUser user;
                if (existingUser is not null && !existingUser.IsDeleted)
                {
                    // User already exists — just add them to medical center role and create profile
                    user = existingUser;
                    user.PhoneNumber = request.PhoneNumber;

                    // Only add role if not already in it
                    if (!await _userManager.IsInRoleAsync(user, "MedicalCenter"))
                    {
                        await _userManager.AddToRoleAsync(user, "MedicalCenter");
                    }

                    // Check if medical center profile already exists
                    var existingMedicalCenter = await _medicalCenterRepository
                        .GetOneAsync(mc => mc.UserId == user.Id);

                    if (existingMedicalCenter is null)
                    {
                        await _medicalCenterRepository.AddAsync(new MedicalCenter
                        {
                            UserId = user.Id,
                            FacilityName = request.FacilityName,
                            FacilityType = request.FacilityType,
                            Address = request.Address,
                            Latitude = request.Latitude,
                            Longitude = request.Longitude,
                            WorkingHours = request.WorkingHours,
                            IsVerified = false,   // Admin sets true on approval
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    // Update existing user if needed
                    await _userManager.UpdateAsync(user);
                }
                else
                {
                    // New user — create in AspNetUsers and add profile
                    user = BuildApplicationUser(request, UserType.MedicalCenter);
                    user.PhoneNumber = request.PhoneNumber;
                    user.EmailConfirmed = false;  // inactive until Admin approves

                    var result = await _userManager.CreateAsync(user, request.Password);
                    if (!result.Succeeded)
                        return ServiceResult.Fail(
                            string.Join(", ", result.Errors.Select(e => e.Description)));

                    await _userManager.AddToRoleAsync(user, "MedicalCenter");

                    await _medicalCenterRepository.AddAsync(new MedicalCenter
                    {
                        UserId = user.Id,
                        FacilityName = request.FacilityName,
                        FacilityType = request.FacilityType,
                        Address = request.Address,
                        Latitude = request.Latitude,
                        Longitude = request.Longitude,
                        WorkingHours = request.WorkingHours,
                        IsVerified = false,   // Admin sets true on approval
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _emailSender.SendEmailAsync(
                    "admin@shifaa.com",
                    "New Medical Center Registration — Pending Approval",
                    $"<h2>Facility registration request</h2>" +
                    $"<p><b>Name:</b> {request.FacilityName}</p>" +
                    $"<p><b>Type:</b> {request.FacilityType}</p>" +
                    $"<p><b>Email:</b> {user.Email}</p>" +
                    $"<p>Please review and approve from the Admin dashboard.</p>");

                return ServiceResult.Ok(
                    "Your registration request has been submitted and is pending Admin review. " +
                    "You will be notified once your facility is verified.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering medical center: {Message}", ex.Message);
                await transaction.RollbackAsync();
                return ServiceResult.Fail($"Registration failed: {ex.Message}", 500);
            }
        }

        // ── LOGIN ─────────────────────────────────────────────────────────
        public async Task<ServiceResult<AuthenticatedResponse>> LoginAsync(LoginRequest request)
        {
            var user = await _userManager.FindByNameAsync(request.UserNameOrEmail)
                ?? await _userManager.FindByEmailAsync(request.UserNameOrEmail);

            if (user is null || user.IsDeleted)
                return ServiceResult<AuthenticatedResponse>.Fail(
                    "Invalid email/username or password.", 401);

            if (!user.EmailConfirmed)
                return ServiceResult<AuthenticatedResponse>.Fail(
                    "Please confirm your email before logging in.", 401);

            var result = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!result)
                return ServiceResult<AuthenticatedResponse>.Fail(
                    "Invalid email/username or password.", 401);

            // Get all user roles
            var userRoles = await _userManager.GetRolesAsync(user);

            // Map role names to UserType enum
            var availableRoles = MapRolesToUserType(userRoles.ToList());

            // Validate that user has the requested role
            var selectedRoleString = request.Role.ToString();

            if (!userRoles.Contains(selectedRoleString))
                return ServiceResult<AuthenticatedResponse>.Fail(
                    $"User does not have the {selectedRoleString} role.", 400);

            // Generate token with selected role only
            var accessToken = await _jwtHandler.GenerateAccessTokenAsync(user, selectedRoleString);
            var refreshToken = _jwtHandler.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _userManager.UpdateAsync(user);

            return ServiceResult<AuthenticatedResponse>.Ok(
                new AuthenticatedResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    SelectedRole = request.Role,
                    AvailableRoles = availableRoles,
                    RedirectUrl = GetRedirectUrl(request.Role)
                },
                "Login successful.");
        }


        // ── LOGOUT ─────────────────────────────────────────────────────────
        public async Task<ServiceResult<AuthenticatedResponse>> LogoutAsync()
        {
            await _signInManager.SignOutAsync();
            return ServiceResult<AuthenticatedResponse>.Ok(
                new AuthenticatedResponse
                {
                    AccessToken = null,
                    RefreshToken = null,
                    AvailableRoles = null,
                    RedirectUrl = null
                },
                "Logout successful.");
        }

        // ── FORGOT PASSWORD ───────────────────────────────────────────────
        public async Task<ServiceResult> ForgotPasswordAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null)
                return ServiceResult.Ok(
                    "If an account with that email exists, an OTP has been sent.");

            if (user.IsDeleted)
                return ServiceResult.Ok(
                    "If an account with that email exists, an OTP has been sent.");

            // Check rate limit: max 10 OTP requests per 24 hours per user
            var recentOtps = await _otpRepository
                .GetAsync(otp => otp.ApplicationUserId == user.Id &&
                                 otp.CreatedAt > DateTime.UtcNow.AddHours(-24));

            if (recentOtps?.Count() >= 10)
                return ServiceResult.Fail(
                    "Too many OTP requests. Please try again tomorrow.", 429);

            // Generate 6-digit OTP
            var otp = new Random().Next(100000, 999999).ToString();

            // Save OTP to database
            var otpRecord = new ApplicationUserOTP(otp, user.Id);
            await _otpRepository.AddAsync(otpRecord);
            await _otpRepository.CommitAsync();

            // Send OTP via email
            await _emailSender.SendEmailAsync(
                user.Email,
                "Shifaa Password Reset OTP",
                $"<h2>Password Reset Request</h2>" +
                $"<p>Your OTP for password reset is:</p>" +
                $"<h3 style='color: #1976d2;'>{otp}</h3>" +
                $"<p>This OTP expires in 10 minutes.</p>" +
                $"<p>If you did not request this, please ignore this email.</p>");

            return ServiceResult.Ok(
                "If an account with that email exists, an OTP has been sent.");
        }

        // ── VALIDATE OTP ──────────────────────────────────────────────────
        public async Task<ServiceResult> ValidateOtpAsync(ValidateOTPRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.ApplicationUserId);
            if (user is null || user.IsDeleted)
                return ServiceResult.Fail("User not found.", 404);

            var otpRecord = await _otpRepository
                .GetOneAsync(otp => otp.ApplicationUserId == request.ApplicationUserId &&
                                    otp.OTP == request.OTP &&
                                    otp.IsValid &&
                                    otp.ExpireAt > DateTime.UtcNow);

            if (otpRecord is null)
                return ServiceResult.Fail("Invalid or expired OTP.", 400);

            // Mark OTP as used
            otpRecord.IsValid = false;
            _otpRepository.Update(otpRecord);
            await _otpRepository.CommitAsync();

            return ServiceResult.Ok("OTP validated successfully.");
        }

        // ── RESET PASSWORD ────────────────────────────────────────────────
        public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.ApplicationUserId);
            if (user is null || user.IsDeleted)
                return ServiceResult.Fail("User not found.", 404);

            // Verify there's a valid, used OTP for this user (meaning they passed OTP validation)
            var usedOtp = await _otpRepository
                .GetOneAsync(otp => otp.ApplicationUserId == request.ApplicationUserId &&
                                    !otp.IsValid &&
                                    otp.ExpireAt > DateTime.UtcNow.AddMinutes(-10));

            if (usedOtp is null)
                return ServiceResult.Fail(
                    "OTP validation required. Please validate your OTP first.", 400);

            // Remove old token and set new password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

            if (!result.Succeeded)
                return ServiceResult.Fail(
                    string.Join(", ", result.Errors.Select(e => e.Description)), 400);

            return ServiceResult.Ok("Password reset successfully. You can now log in.");
        }

        // ── REFRESH TOKEN ─────────────────────────────────────────────────
        public async Task<ServiceResult<AuthenticatedResponse>> RefreshTokenAsync(TokenApiRequest request)
        {
            if (string.IsNullOrEmpty(request.AccessToken) || string.IsNullOrEmpty(request.RefreshToken))
                return ServiceResult<AuthenticatedResponse>.Fail(
                    "Invalid tokens.", 401);

            var principal = _jwtHandler.GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal is null)
                return ServiceResult<AuthenticatedResponse>.Fail(
                    "Invalid token.", 401);
            
            var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return ServiceResult<AuthenticatedResponse>.Fail(
                    "Invalid token.", 401);

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || user.IsDeleted)
                return ServiceResult<AuthenticatedResponse>.Fail(
                    "User not found.", 404);

            if (user.RefreshToken != request.RefreshToken ||
                user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                return ServiceResult<AuthenticatedResponse>.Fail(
                    "Invalid refresh token or expired.", 401);

            var newAccessToken = await _jwtHandler.GenerateAccessTokenAsync(user);
            var newRefreshToken = _jwtHandler.GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _userManager.UpdateAsync(user);

            return ServiceResult<AuthenticatedResponse>.Ok(
                new AuthenticatedResponse
                {
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken
                },
                "Tokens refreshed successfully.");
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────────

        private ApplicationUser BuildApplicationUser(
            BaseRegisterRequest request, UserType userType)
        {
            return new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                UserType = userType,
                Language = "ar",
                EmailConfirmed = false,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task SendEmailConfirmationAsync(ApplicationUser user)
        {
            var token = await _userManager
                .GenerateEmailConfirmationTokenAsync(user);

            await _emailSender.SendEmailAsync(
                user.Email,
                "Confirm your Shifaa account",
                $"<h2>Welcome to Shifaa!</h2>" +
                $"<p>Please confirm your email address to activate your account.</p>" +
                $"<p>Your confirmation token: <b>{token}</b></p>");
        }

        public async Task<ServiceResult> ConfirmEmailAsync(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || user.IsDeleted)
                return ServiceResult.Fail("User not found.", 404);

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
                return ServiceResult.Fail(
                    string.Join(", ", result.Errors.Select(e => e.Description)), 400);

            return ServiceResult.Ok("Email confirmed successfully. You can now log in.");
        }

        /// <summary>
        /// Cleans up the ApplicationUser from Identity if the profile
        /// table insert failed inside the transaction — keeps DB consistent.
        /// </summary>
        private async Task CleanupUserAsync(ApplicationUser user)
        {
            var existingUser = await _userManager.FindByIdAsync(user.Id);
            if (existingUser is not null)
                await _userManager.DeleteAsync(existingUser);
        }

        /// <summary>
        /// Maps role names to UserType enum values
        /// </summary>
        private List<UserType> MapRolesToUserType(List<string> roles)
        {
            var userTypes = new List<UserType>();
            foreach (var role in roles)
            {
                if (Enum.TryParse<UserType>(role, out var userType))
                {
                    userTypes.Add(userType);
                }
            }
            return userTypes;
        }

        /// <summary>
        /// Determines redirect URL based on user role
        /// </summary>
        private string GetRedirectUrl(UserType role)
        {
            return role switch
            {
                UserType.Member => "/member/dashboard",
                UserType.Caregiver => "/caregiver/dashboard",
                UserType.Doctor => "/doctor/dashboard",
                UserType.MedicalCenter => "/medical-center/dashboard",
                _ => "/"
            };
        }

        /// <summary>
        /// Switches user to a different role for the current session
        /// </summary>
        public async Task<ServiceResult<AuthenticatedResponse>> SwitchRoleAsync(
            string userId, UserType newRole)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || user.IsDeleted)
                return ServiceResult<AuthenticatedResponse>.Fail("User not found.", 404);

            var userRoles = await _userManager.GetRolesAsync(user);
            var newRoleString = newRole.ToString();

            // Validate that user has the requested role
            if (!userRoles.Contains(newRoleString))
                return ServiceResult<AuthenticatedResponse>.Fail(
                    $"User does not have the {newRoleString} role.", 400);

            // Generate new token with the new role
            var accessToken = await _jwtHandler.GenerateAccessTokenAsync(user, newRoleString);
            var availableRoles = MapRolesToUserType(userRoles.ToList());

            return ServiceResult<AuthenticatedResponse>.Ok(
                new AuthenticatedResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = user.RefreshToken, // Keep existing refresh token
                    SelectedRole = (UserType)(int)newRole,
                    AvailableRoles = availableRoles,
                    RedirectUrl = GetRedirectUrl(newRole)
                },
                "Role switched successfully.");
        }
    }
}
