using BinDays.Api.Incidents;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Use Autofac as the service provider factory
builder.Host.UseServiceProviderFactory(new Autofac.Extensions.DependencyInjection.AutofacServiceProviderFactory());

// Register services directly with Autofac using the ConfigureContainer method
builder.Host.ConfigureContainer<Autofac.ContainerBuilder>(BinDays.Api.Initialisation.DependencyInjection.ConfigureContainer);

builder.Services.AddControllers();

// Add caching for responses and incidents, either in-memory or Redis
var redis = builder.Configuration.GetValue<string>("Redis");
if (!string.IsNullOrEmpty(redis))
{
	builder.Services.AddStackExchangeRedisCache(options =>
	{
		options.Configuration = redis;
	});

	builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
	builder.Services.AddSingleton<IIncidentStore, RedisIncidentStore>();
}
else
{
	builder.Services.AddDistributedMemoryCache();
	builder.Services.AddSingleton<IIncidentStore, InMemoryIncidentStore>();
}

// Health check for monitoring
builder.Services.AddHealthChecks();

// Configure Seq logging (optional)
builder.Services.AddLogging(loggingBuilder =>
{
	loggingBuilder.AddSeq(builder.Configuration.GetSection("Seq"));
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseCors(x => x
	.AllowAnyOrigin()
	.AllowAnyMethod()
	.AllowAnyHeader()
);

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/status");

app.Run();
