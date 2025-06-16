using EReaderApp.Data;
using EReaderApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            }
            else
            {
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

                string connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
                    ?? builder.Configuration.GetConnectionString("SupabaseConnection");

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

            // Add services
            builder.Services.AddSingleton<StorageService>();
            builder.Services.AddScoped<IJwtService, JwtService>();

            builder.Services.AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    // Configure JSON serialization for APIs
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                });

            // Configure CORS for mobile apps
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("MobileAppPolicy", policy =>
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        // Development: Allow any origin for testing
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    }
                    else
                    {
                        // Production: Specify allowed origins
                        var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")?.Split(',')
                            ?? new[] { "https://yourdomain.com" };

                        policy.WithOrigins(allowedOrigins)
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    }
                });

                // Separate policy for web app (if needed)
                options.AddPolicy("WebAppPolicy", policy =>
                {
                    policy.WithOrigins("https://localhost:7166", "https://yourdomain.com")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            // Configure authentication with both Cookie and JWT
            var googleClientId = builder.Environment.IsProduction()
                ? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                : builder.Configuration["Authentication:Google:ClientId"];

            var googleClientSecret = builder.Environment.IsProduction()
                ? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
                : builder.Configuration["Authentication:Google:ClientSecret"];

            // Get JWT configuration
            var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                ?? builder.Configuration["Jwt:SecretKey"];

            if (string.IsNullOrEmpty(jwtSecretKey))
            {
                throw new InvalidOperationException("JWT Secret Key must be configured for production.");
            }

            builder.Services.AddAuthentication(options =>
            {
                // Default scheme for web controllers
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
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
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = builder.Environment.IsProduction();
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? builder.Configuration["Jwt:Issuer"] ?? "EReaderApp",
                    ValidateAudience = true,
                    ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? builder.Configuration["Jwt:Audience"] ?? "EReaderApp",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Handle JWT token from query string for SignalR connections (if you add real-time features)
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
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

            // Configure authorization policies
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireAdminRole", policy =>
                    policy.RequireRole("Admin")
                          .RequireAuthenticatedUser());

                // Policy that accepts both authentication schemes
                options.AddPolicy("ApiOrWeb", policy =>
                    policy.AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        CookieAuthenticationDefaults.AuthenticationScheme)
                          .RequireAuthenticatedUser());
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
                    context.Database.Migrate();
                    app.Logger.LogInformation("PostgreSQL database migrations applied successfully");
                }
                else
                {
                    context.Database.EnsureCreated();
                    app.Logger.LogInformation("SQL Server database ensured");
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error with database setup");
                if (app.Environment.IsDevelopment())
                {
                    throw;
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

            // CORS must be after UseRouting and before UseAuthentication
            app.UseCors("MobileAppPolicy");

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();

            // Configure routes
            app.MapControllerRoute(
                name: "api",
                pattern: "api/{controller=Home}/{action=Index}/{id?}");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}