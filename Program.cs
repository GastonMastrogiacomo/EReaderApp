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

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                });

            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddControllersWithViews();

            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("MobileAppPolicy", policy =>
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    }
                    else
                    {
                        var allowedOrigins = new List<string>
                        {
                            "https://librolibredv.onrender.com",
                            "http://localhost:3000",
                            "http://10.0.2.2:5000"
                        };

                        var customOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
                        if (!string.IsNullOrEmpty(customOrigins))
                        {
                            allowedOrigins.AddRange(customOrigins.Split(','));
                        }

                        policy.WithOrigins(allowedOrigins.ToArray())
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    }
                });
            });

            // Authentication Configuration
            var googleClientId = builder.Environment.IsProduction()
                ? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                : builder.Configuration["Authentication:Google:ClientId"];

            var googleClientSecret = builder.Environment.IsProduction()
                ? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
                : builder.Configuration["Authentication:Google:ClientSecret"];

            // Get JWT configuration
            var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                ?? builder.Configuration["Jwt:SecretKey"]
                ?? "YourDefaultSecretKeyForDevelopment_MustBe32Characters!";

            var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
                ?? builder.Configuration["Jwt:Issuer"]
                ?? "EReaderApp";

            var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
                ?? builder.Configuration["Jwt:Audience"]
                ?? "EReaderApp";

            // Configure Authentication with proper scheme hierarchy
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
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
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/api")))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogInformation("JWT token validated for user: {UserId}",
                            context.Principal?.FindFirst("user_id")?.Value);
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogWarning("JWT authentication failed: {Exception}", context.Exception.Message);
                        return Task.CompletedTask;
                    }
                };
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
            .AddGoogle(GoogleDefaults.AuthenticationScheme, googleOptions =>
            {
                if (string.IsNullOrEmpty(googleClientId))
                {
                    builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);
                    var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("Google Client ID is not configured. Google authentication will not work.");
                    googleOptions.ClientId = "placeholder";
                    googleOptions.ClientSecret = "placeholder";
                }
                else
                {
                    googleOptions.ClientId = googleClientId;
                    googleOptions.ClientSecret = googleClientSecret ?? throw new InvalidOperationException("Google Client Secret is not configured");
                }

                googleOptions.CallbackPath = "/signin-google";
                googleOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                googleOptions.SaveTokens = true;

                googleOptions.Scope.Clear();
                googleOptions.Scope.Add("openid");
                googleOptions.Scope.Add("profile");
                googleOptions.Scope.Add("email");
            });

            // Configure authorization policies
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireAdminRole", policy =>
                    policy.RequireRole("Admin")
                          .RequireAuthenticatedUser()
                          .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme));

                options.AddPolicy("ApiOrWeb", policy =>
                    policy.AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        CookieAuthenticationDefaults.AuthenticationScheme)
                          .RequireAuthenticatedUser());

                options.AddPolicy("ApiOnly", policy =>
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
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

            if (app.Environment.IsProduction())
            {
                app.Use((context, next) =>
                {
                    context.Request.Scheme = "https";
                    return next();
                });
            }

            app.UseRouting();
            app.UseCors("MobileAppPolicy");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();

            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    app.Logger.LogInformation("API Request: {Method} {Path} from {Host}",
                        context.Request.Method,
                        context.Request.Path,
                        context.Request.Host);
                }
                await next();
            });

            app.MapControllers();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}