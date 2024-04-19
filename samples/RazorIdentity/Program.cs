using Amazon.DynamoDBv2;
using AspNetCore.Identity.AmazonDynamoDB;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Services
  .AddDefaultAWSOptions(builder.Configuration.GetAWSOptions())
  .AddSingleton<IAmazonDynamoDB>(
    builder.Environment.IsDevelopment() ?
    new AmazonDynamoDBClient(
        new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:8000"
        }
    ) :
    new()
);
builder.Services
    .AddIdentity<DynamoDbUser, DynamoDbRole>()
    .AddDefaultUI()
    .AddDynamoDbStores()
    .Configure(options =>
    {
        options.DefaultTableName = "identity-samples-identity-api";
    });
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapIdentityApi<DynamoDbUser>();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

// Should not be run during startup in production, move to setup script
AspNetCoreIdentityDynamoDbSetup.EnsureInitialized(app.Services);

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
