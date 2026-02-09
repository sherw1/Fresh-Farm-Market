using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace AS_Assignment2.Middleware
{
    public class SessionValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AppDbContext db)
        {
            // Skip validation for public pages
            var path = context.Request.Path.Value?.ToLower() ?? "";
            if (path.Contains("/account/login") || 
                path.Contains("/account/register") ||
                path.Contains("/account/forgotpassword") ||
                path.Contains("/account/resetpassword") ||
                path.Contains("/notfound") ||
                path.Contains("/error") ||
                path.Contains("/css/") ||
                path.Contains("/js/") ||
                path.Contains("/lib/"))
            {
                await _next(context);
                return;
            }

            // Only check authenticated requests
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var sessionId = context.Session.GetString("SessionId");

                if (!string.IsNullOrEmpty(sessionId))
                {
                    // Verify session exists and is active
                    var userSession = await db.UserSessions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);

                    if (userSession == null)
                    {
                        // Session not found or inactive - force logout
                        context.Session.Clear();
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        context.Response.Redirect("/Account/Login");
                        return;
                    }

                    // Check session timeout (2 minute)
                    var sessionAge = DateTime.UtcNow - userSession.LastActivityTime;
                    if (sessionAge.TotalMinutes > 1)
                    {
                        // Session expired - deactivate
                        var sessionToUpdate = await db.UserSessions
                            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
                        if (sessionToUpdate != null)
                        {
                            sessionToUpdate.IsActive = false;
                            await db.SaveChangesAsync();
                        }
                        
                        context.Session.Clear();
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        context.Response.Redirect("/Account/Login");
                        return;
                    }
                }
                else
                {
                    // Authenticated but no session ID - force logout
                    context.Session.Clear();
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    context.Response.Redirect("/Account/Login");
                    return;
                }
            }

            await _next(context);
        }
    }

    public static class SessionValidationMiddlewareExtensions
    {
        public static IApplicationBuilder UseSessionValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionValidationMiddleware>();
        }
    }
}
