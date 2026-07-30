using ClientServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<ServerApiOptions>(builder.Configuration.GetSection("ServerApi"));
builder.Services.AddHttpClient<IServerProbeService, ServerProbeService>((serviceProvider, client) =>
{
	var options = serviceProvider
		.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServerApiOptions>>()
		.Value;

	client.BaseAddress = new Uri(options.BaseUrl);
	client.Timeout = TimeSpan.FromSeconds(5);
});

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

app.Run();
