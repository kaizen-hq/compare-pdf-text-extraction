using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Web.Services;

public class PyMuPdfService(HttpClient httpClient) : HttpClientPdfReaderService(httpClient, "py-mu-pdf");
public class PdfPigService(HttpClient httpClient) : HttpClientPdfReaderService(httpClient, "pdf-pig");

public record ExtractionResult(string Text, TimeSpan ElapsedTime);

public abstract class HttpClientPdfReaderService(HttpClient httpClient, string path)
{
    public async Task<ExtractionResult> ExtractText(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Create multipart form data content
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            content.Add(streamContent, "file", fileName);

            // POST to PDF reader service
            var response = await httpClient.PostAsync($"/{path}", content, cancellationToken);

            stopwatch.Stop();

            // Check if request was successful
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"PDF reader service returned error: {response.StatusCode}. {errorContent}",
                    null,
                    response.StatusCode);
            }

            // Parse JSON response
            var result = await response.Content.ReadFromJsonAsync<PdfExtractionResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("PDF reader service returned null response");
            }

            if (!result.Success)
            {
                var errorMessage = result.Error ?? "Unknown error occurred during PDF extraction";
                throw new InvalidOperationException($"PDF extraction failed: {errorMessage}");
            }

            if (string.IsNullOrWhiteSpace(result.Text))
            {
                throw new InvalidOperationException("PDF extraction returned empty text");
            }

            return new ExtractionResult(result.Text, stopwatch.Elapsed);
        }
        catch (HttpRequestException)
        {
            throw; // Re-throw HTTP exceptions as-is
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new TimeoutException("PDF extraction timed out. The file may be too large or the service is unavailable.", ex);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("PDF extraction was cancelled.", ex, ex.CancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse response from PDF reader service.", ex);
        }
    }
}

internal record PdfExtractionResponse
{
    public bool Success { get; init; }
    public string? Text { get; init; }
    public int Pages { get; init; }
    public string? Filename { get; init; }
    public string? Error { get; init; }
}