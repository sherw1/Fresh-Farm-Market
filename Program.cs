using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using AS_Assignment2;
using AS_Assignment2.Services;
using AS_Assignment2.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Register HttpClient for ReCaptcha
builder.Services.AddHttpClient<ReCaptchaService>();

// Register Email Service
builder.Services.AddScoped<EmailService>();

// Register DbContext with retry logic and connection resiliency
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

// Data protection for encrypting sensitive fields
builder.Services.AddDataProtection();

// Cookie authentication for session
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(1); // ? Changed from 20 to 1 minute
        options.Cookie.HttpOnly = true;
        // Use SameAsRequest for development, Always for production
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
            ? CookieSecurePolicy.SameAsRequest 
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                // Validate the session exists and is active
                var sessionId = context.HttpContext.Session.GetString("SessionId");
                if (!string.IsNullOrEmpty(sessionId))
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var session = await db.UserSessions
                        .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);
                    
                    if (session == null)
                    {
                        // Session not found or inactive - reject the principal
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                }
                else if (context.Principal?.Identity?.IsAuthenticated == true)
                {
                    // Authenticated but no session - reject
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(1); // ? Changed from 20 to 1 minute
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Antiforgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while creating the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Custom 404 handling
app.UseStatusCodePagesWithReExecute("/NotFound");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Add middleware to prevent caching of authenticated pages
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "-1";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    await next();
});

// IMPORTANT: Session must be enabled BEFORE authentication
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Add session validation middleware AFTER authentication
app.UseSessionValidation();

app.MapRazorPages();

app.Run();
