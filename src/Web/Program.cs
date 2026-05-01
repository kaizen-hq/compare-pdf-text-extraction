using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Web.Components;
using Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Pure SSR — no interactive render modes.
builder.Services.AddRazorComponents();

builder.Services.AddMemoryCache();

const long maxUploadBytes = 100L * 1024 * 1024;
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxUploadBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxUploadBytes);

builder.Services.AddHttpClient<PyMuPdfService>(client =>
{
    client.BaseAddress = new("https+http://python-api");
    client.Timeout = TimeSpan.FromMinutes(1);
});

builder.Services.AddHttpClient<PdfPigService>(client =>
{
    client.BaseAddress = new("https+http://csharp-api");
    client.Timeout = TimeSpan.FromMinutes(1);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.MapPost("/api/compare", async (
    IFormFile pdf,
    PyMuPdfService pyMuPdf,
    PdfPigService pdfPig,
    IMemoryCache cache,
    CancellationToken ct) =>
{
    using var pyStream = new MemoryStream();
    using var pigStream = new MemoryStream();
    await using (var src = pdf.OpenReadStream())
    {
        await src.CopyToAsync(pyStream, ct);
    }
    pyStream.Position = 0;
    await pyStream.CopyToAsync(pigStream, ct);
    pyStream.Position = 0;
    pigStream.Position = 0;

    var pyTask = pyMuPdf.ExtractText(pyStream, pdf.FileName, ct);
    var pigTask = pdfPig.ExtractText(pigStream, pdf.FileName, ct);
    await Task.WhenAll(pyTask, pigTask);

    var id = Guid.NewGuid().ToString("N");
    cache.Set(id, new CompareResult(pdf.FileName, await pyTask, await pigTask),
        TimeSpan.FromMinutes(15));

    return Results.Redirect($"/?id={id}");
});

app.MapDefaultEndpoints();
app.Run();

public sealed record CompareResult(string FileName, ExtractionResult PyMuPdf, ExtractionResult PdfPig);
