using System;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.AspNetCore.Http.HttpResults;
using CsharpApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateSlimBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/pdf-pig", async (HttpRequest req, CancellationToken cancellationToken) =>
{
    try
    {
        if (!req.HasFormContentType)
            return Results.BadRequest(new { Success = false, Error = "Expected multipart/form-data with a file field." });

        var form = await req.ReadFormAsync(cancellationToken);
        var file = form.Files["file"] ?? form.Files.FirstOrDefault();
        if (file == null)
            return Results.BadRequest(new { Success = false, Error = "No file provided." });

        await using var stream = file.OpenReadStream();
        var service = new PdfPigPdfReaderService();
        var text = await service.ExtractText(stream, file.FileName, cancellationToken);

        var response = new
        {
            Success = true,
            Text = text,
            Pages = 0,
            Filename = file.FileName,
            Error = (string?)null
        };

        return Results.Ok(response);
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (Exception ex)
    {
        var errorResp = new { Success = false, Text = (string?)null, Pages = 0, Filename = (string?)null, Error = ex.Message };
        return Results.BadRequest(errorResp);
    }
});

Todo[] sampleTodos =
[
    new(1, "Walk the dog"),
    new(2, "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
    new(3, "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
    new(4, "Clean the bathroom"),
    new(5, "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
];

var todosApi = app.MapGroup("/todos");
todosApi.MapGet("/", () => sampleTodos)
        .WithName("GetTodos");

todosApi.MapGet("/{id}", Results<Ok<Todo>, NotFound> (int id) =>
    sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
        ? TypedResults.Ok(todo)
        : TypedResults.NotFound())
    .WithName("GetTodoById");

app.MapDefaultEndpoints();
app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
