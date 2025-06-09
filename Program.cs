using EReaderApp.Data;
using EReaderApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;

namespace EReaderApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure database based on environment
            if (builder.Environment.IsDevelopment())
            {
                // Use SQL Server for local development
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            }
            else
            {
                // Use PostgreSQL (Supabase) for production
                string connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
                    ?? builder.Configuration.GetConnectionString("SupabaseConnection");

                Console.WriteLine($"Is Production: {builder.Environment.IsProduction()}");
                Console.WriteLine($"Environment Name: {builder.Environment.EnvironmentName}");

                Console.WriteLine($"Connection string from env: '{Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")}'");
                Console.WriteLine($"Final connection string: '{connectionString}'");
                Console.WriteLine($"Connection string length: {connectionString?.Length ?? 0}");

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Supabase connection string is not configured for production.");
                }

                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorCodesToAdd: null);
                        npgsqlOptions.CommandTimeout(120);
                    }));
            }

            // Add storage service
            builder.Services.AddSingleton<StorageService>();

            builder.Services.AddControllersWithViews();

            // Configure authentication
            var googleClientId = builder.Environment.IsProduction()
                ? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                : builder.Configuration["Authentication:Google:ClientId"];

            var googleClientSecret = builder.Environment.IsProduction()
                ? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
                : builder.Configuration["Authentication:Google:ClientSecret"];

            builder.Services.AddAuthentication(options => {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/Auth/Login";
                options.LogoutPath = "/Auth/Logout";
                options.AccessDeniedPath = "/Auth/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = builder.Environment.IsProduction()
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
            })
            .AddGoogle(googleOptions =>
            {
                googleOptions.ClientId = googleClientId;
                googleOptions.ClientSecret = googleClientSecret;
                googleOptions.CallbackPath = "/signin-google";

                googleOptions.Scope.Add("https://www.googleapis.com/auth/userinfo.email");
                googleOptions.Scope.Add("https://www.googleapis.com/auth/userinfo.profile");
                googleOptions.Scope.Add("openid");
            });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
            });

            // Configure session
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = builder.Environment.IsProduction()
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
            });

            var app = builder.Build();

            // Apply database migrations on startup
            try
            {
                using var scope = app.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                if (app.Environment.IsProduction())
                {
                    // For production (PostgreSQL), apply migrations
                    context.Database.Migrate();
                    app.Logger.LogInformation("PostgreSQL database migrations applied successfully");
                }
                else
                {
                    // For development (SQL Server), ensure database is created
                    context.Database.EnsureCreated();
                    app.Logger.LogInformation("SQL Server database ensured");
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error with database setup");
                // Don't throw in production - let the app start and show health check errors
                if (app.Environment.IsDevelopment())
                {
                    throw; // In development, we want to see the error immediately
                }
            }

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            // Security headers
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                await next();
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
                }
            });

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}