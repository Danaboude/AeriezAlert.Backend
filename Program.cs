using AeriezAlert.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Reduce noise in logs
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("AeriezAlert", LogLevel.Information); // Keep our app logs visible

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Custom Services
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<UserLookupService>();
builder.Services.AddSingleton<PhoneNotificationService>();
builder.Services.AddSingleton<DaemonService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DaemonService>());

// CORS policy to allow calls 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseDefaultFiles(); // Serves index.html by default
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
