

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Shifaa.JwtFeatures;
using Shifaa.Services;

namespace Shifaa
{
    public static class AppConfiguration
    {

        public static void Config(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                //options.UseSqlServer(builder.Configuration.GetSection("ConnectionStrings")["DefaultConnection"]); 
                //options.UseSqlServer(builder.Configuration["ConnectionStrings : DefaultConnection"]); 
                //options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")); 
                options.UseSqlServer(connectionString);
            });
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedEmail = true;
            })
                .AddDefaultTokenProviders()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = $"/Identity/Account/Login";
                options.AccessDeniedPath = $"/Identity/Account/AccessDenied";

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = 403;
                    return Task.CompletedTask;
                };

            });

            services.AddTransient<IEmailSender, EmailSender>();
            
            // Healthcare Models Repositories (Shifaa)
            services.AddScoped<IRepository<Member>, Repository<Member>>();
            services.AddScoped<IRepository<Doctor>, Repository<Doctor>>();
            services.AddScoped<IRepository<MedicalCenter>, Repository<MedicalCenter>>();
            services.AddScoped<IRepository<Caregiver>, Repository<Caregiver>>();
            services.AddScoped<IRepository<DoctorSchedule>, Repository<DoctorSchedule>>();
            services.AddScoped<IRepository<TimeSlot>, Repository<TimeSlot>>();
            services.AddScoped<IRepository<Appointment>, Repository<Appointment>>();
            services.AddScoped<IRepository<Prescription>, Repository<Prescription>>();
            services.AddScoped<IRepository<PrescriptionItem>, Repository<PrescriptionItem>>();
            services.AddScoped<IRepository<MedicalRecord>, Repository<MedicalRecord>>();
            services.AddScoped<IRepository<HealthReading>, Repository<HealthReading>>();
            services.AddScoped<IRepository<AIAlert>, Repository<AIAlert>>();
            services.AddScoped<IRepository<TrackingLog>, Repository<TrackingLog>>();
            services.AddScoped<IRepository<Notification>, Repository<Notification>>();
            services.AddScoped<IRepository<Review>, Repository<Review>>();
            services.AddScoped<IRepository<GuardianMember>, Repository<GuardianMember>>();
            services.AddScoped<IRepository<ApplicationUserOTP>, Repository<ApplicationUserOTP>>();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<IJwtHandler, JwtHandler>();
            services.AddScoped<IDBInitializr, DBInitializr>();
        }
    }
}
