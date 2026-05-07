using FireflyFramework.ConfigServer;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddFireflyConfigServer(builder.Configuration);

var app = builder.Build();
app.MapFireflyConfigServer();
app.MapGet("/", () => "Firefly Config Server (.NET) — Spring-Cloud-Config compatible.");
app.Run();
