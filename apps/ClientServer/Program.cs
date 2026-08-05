using System.Reflection;
using ClientServer.Services;
using ClientServer.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
	var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
	options.IncludeXmlComments(xmlPath);
});
builder.Services.AddSignalR();

builder.Services.Configure<MainServerApiOptions>(builder.Configuration.GetSection(MainServerApiOptions.SectionName));
builder.Services.Configure<MockDeviceApiOptions>(builder.Configuration.GetSection(MockDeviceApiOptions.SectionName));
builder.Services.Configure<IoTControllerOptions>(builder.Configuration.GetSection(IoTControllerOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddSingleton<IJwtTokenProvider, JwtTokenProvider>();
builder.Services.AddSingleton<IControllerStateStore, ControllerStateStore>();
builder.Services.AddScoped<BearerTokenHandler>();

builder.Services.AddHttpClient<IMainServerTopologyClient, MainServerTopologyClient>((serviceProvider, client) =>
{
	var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MainServerApiOptions>>().Value;
	client.BaseAddress = new Uri(options.BaseUrl);
	client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddHttpClient<IMockDeviceClient, MockDeviceClient>((serviceProvider, client) =>
{
	var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MockDeviceApiOptions>>().Value;
	client.BaseAddress = new Uri(options.BaseUrl);
	client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHostedService<IoTControllerBackgroundService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Ok(new
{
	service = "ClientServer",
	status = "ok",
	docs = "/swagger"
}));

app.MapControllers();
app.MapHub<ControllerHub>("/controllerHub");

app.Run();
