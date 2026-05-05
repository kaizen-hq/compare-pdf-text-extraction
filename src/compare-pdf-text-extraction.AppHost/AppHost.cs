using Aspire.Hosting.Publishing;

var builder = DistributedApplication.CreateBuilder(args);
const string appName = "compare-pdf-text-extraction";
#pragma warning disable ASPIREPIPELINES003
builder.AddDockerComposeEnvironment(appName)
    .WithDashboard(db => db.WithHostPort(8086))
    .WithSshDeploySupport()
    .WithImagePullPolicy(PullPolicy.Missing)
    .WithPullRegistry("localhost:5001");

var pythonApi = builder
    .AddUvicornApp("python-api", "../python_api", "python_api:app")
    .WithUv()
    .WithContainerBuildOptions(context => context.TargetPlatform = ContainerTargetPlatform.LinuxArm64)
    //.WithEndpoint("http", e => e.UriScheme  = builder.ExecutionContext.IsPublishMode ? "https" : "http")
    //.WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .PublishAsDockerComposeService((_, service) => service.Restart = "unless-stopped");

var csharpApi = builder.AddProject<Projects.CsharpApi>("csharp-api")
    .WithContainerBuildOptions(context => context.TargetPlatform = ContainerTargetPlatform.LinuxArm64)
    .WithHttpHealthCheck("/health")
    .PublishAsDockerComposeService((_, service) => service.Restart = "unless-stopped");

builder.AddProject<Projects.Web>("web")
    .WithContainerBuildOptions(context => context.TargetPlatform = ContainerTargetPlatform.LinuxArm64)
    .WithEndpoint("http", e => e.Port = 59436)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(pythonApi)
    .WithReference(csharpApi)
    .WaitFor(pythonApi)
    .WaitFor(csharpApi)
    .PublishAsDockerComposeService((_, service) => service.Restart = "unless-stopped");

builder.Build().Run();
