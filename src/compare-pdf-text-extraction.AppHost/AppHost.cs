var builder = DistributedApplication.CreateBuilder(args);
const string appName = "compare-pdf-text-extraction";

builder.AddDockerComposeEnvironment("env")
    .WithDashboard(db => db.WithHostPort(8085))
    .WithSshDeploySupport();

var pythonApi = builder
    .AddUvicornApp("python-api", "../python_api", "python_api:app")
    .WithUv()
    //.WithEndpoint("http", e => e.UriScheme  = builder.ExecutionContext.IsPublishMode ? "https" : "http")
    //.WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

var csharpApi = builder.AddProject<Projects.CsharpApi>("csharp-api")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Web>("web")
    .WithEndpoint("http", e => e.Port = 59436)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(pythonApi)
    .WithReference(csharpApi)
    .WaitFor(pythonApi)
    .WaitFor(csharpApi);

builder.Build().Run();
