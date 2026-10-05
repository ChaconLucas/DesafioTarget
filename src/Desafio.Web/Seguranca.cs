using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Desafio.Web;

/// <summary>Configurações de segurança HTTP da aplicação. Os motivos de cada uma estão no SECURITY.md.</summary>
public static class Seguranca
{
    public const string PoliticaEscrita = "escrita";

    /// <summary>Tamanho máximo do corpo de uma requisição. Uma movimentação ocupa poucas centenas de bytes.</summary>
    public const long TamanhoMaximoCorpo = 16 * 1024;

    // A página não usa scripts inline nem recursos de terceiros, exceto a fonte Inter do Google Fonts.
    // 'unsafe-inline' em style-src é necessário para as larguras das barras (atributo style).
    private const string Csp =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    public static WebApplicationBuilder AdicionarSeguranca(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.AddServerHeader = false;
            o.Limits.MaxRequestBodySize = TamanhoMaximoCorpo;
        });

        // Respostas de erro em formato ProblemDetails, sem stack trace.
        builder.Services.AddProblemDetails();

        // JSON malformado ou valor inválido (ex.: tipo "Roubo") é erro do cliente: 400, nunca 500.
        // Por padrão o ASP.NET lança exceção nesses casos em Development, o que virava 500 no handler de erros.
        builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);

        // Limita lançamentos e reinícios do estoque por IP, para que um script não encha o histórico em disco.
        var limite = builder.Configuration.GetValue("Seguranca:EscritasPorMinuto", 60);
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(PoliticaEscrita, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1) }));
        });

        return builder;
    }

    public static WebApplication UsarSeguranca(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.Use(async (ctx, next) =>
        {
            var h = ctx.Response.Headers;
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            // O Swagger UI usa scripts inline, então a CSP vale só para a aplicação.
            if (!ctx.Request.Path.StartsWithSegments("/swagger"))
                h.ContentSecurityPolicy = Csp;
            await next();
        });

        app.UseRateLimiter();
        return app;
    }
}
