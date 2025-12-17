using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsharpApi.Services;

public interface IPdfReaderService
{
    Task<string> ExtractText(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}