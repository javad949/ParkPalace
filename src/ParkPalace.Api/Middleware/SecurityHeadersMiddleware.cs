using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ParkPalace.Api.Middleware;

/// <summary>
/// Phase 8 Security Hardening Middleware: Enforces HTTP security headers to protect against
/// clickjacking, MIME sniffing, cross-site scripting (XSS), and unauthorized framing.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            if (!headers.ContainsKey("X-Content-Type-Options"))
            {
                headers.Append("X-Content-Type-Options", "nosniff");
            }

            if (!headers.ContainsKey("X-Frame-Options"))
            {
                headers.Append("X-Frame-Options", "SAMEORIGIN");
            }

            if (!headers.ContainsKey("X-XSS-Protection"))
            {
                headers.Append("X-XSS-Protection", "1; mode=block");
            }

            if (!headers.ContainsKey("Referrer-Policy"))
            {
                headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            }

            if (!headers.ContainsKey("Permissions-Policy"))
            {
                headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            }

            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                headers.Append("Content-Security-Policy", "default-src 'self'; frame-ancestors 'self'; img-src 'self' data:;");
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
