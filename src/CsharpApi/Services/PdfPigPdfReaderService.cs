using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CsharpApi.Services;

public class PdfPigPdfReaderService : IPdfReaderService
{
    public Task<string> ExtractText(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure stream is at the beginning
            if (fileStream.CanSeek && fileStream.Position != 0)
            {
                fileStream.Position = 0;
            }

            using var document = PdfDocument.Open(fileStream);
            
            var textBuilder = new System.Text.StringBuilder();
            
            // Iterate through all pages and extract text
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Extract text from the page using ContentOrderTextExtractor
                // This preserves the reading order, unlike page.Text which uses internal content order
                var pageText = ContentOrderTextExtractor.GetText(page, addDoubleNewline: true);
                
                if (string.IsNullOrWhiteSpace(pageText)) continue;
                textBuilder.AppendLine(pageText);
            }

            var extractedText = textBuilder.ToString().Trim();
            
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidOperationException("PDF extraction returned empty text. The PDF may not contain extractable text.");
            }

            return Task.FromResult(extractedText);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation as-is
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to extract text from PDF: {ex.Message}", ex);
        }
    }
}

