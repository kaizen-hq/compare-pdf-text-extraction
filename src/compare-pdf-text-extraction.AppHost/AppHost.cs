var builder = DistributedApplication.CreateBuilder(args);
const string appName = "compare-pdf-text-extraction";

builder.AddDockerComposeEnvironment("env")
    .WithDashboard(db => db.WithHostPort(8085))
    .WithSshDeploySupport();

var pythonApi = builder
    .AddUvicornApp("python-api", "../python_api", "python_api:app")
    .WithUv()
    //.WithEndpoint("http", e => e.UriScheme  = builder.ExecutionContext.IsPublishMode ? "https" : "http")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

if (!builder.ExecutionContext.IsPublishMode)
    builder.CreateResourceBuilder((ExecutableResource)builder.Resources.First(x => x.Name == $"{pythonApi.Resource.Name}-installer"))
        .WithCertificateTrustScope(CertificateTrustScope.None);

var csharpApi = builder.AddProject<Projects.CsharpApi>("csharp-api");

builder.AddProject<Projects.Web>("web")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(pythonApi)
    .WithReference(csharpApi)
    .WaitFor(pythonApi)
    .WaitFor(csharpApi);

builder.Build().Run();
