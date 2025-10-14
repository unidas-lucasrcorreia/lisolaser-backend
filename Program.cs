using LisoLaser.Backend.Infrastructure.DependencyInjection;
using LisoLaser.Backend.Infrastructure.Errors;

var builder = WebApplication.CreateBuilder(args);

// ===== CORS =====
const string CorsPolicy = "AllowFrontend";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicy, p =>
        p.AllowAnyOrigin()  // <-- Changed from WithOrigins to allow all
         .AllowAnyHeader()
         .AllowAnyMethod());
});

// ===== Integrations =====
builder.Services
    .AddCmsIntegration(builder.Configuration)
    .AddUnobjectIntegration(builder.Configuration);

// ===== API + Swagger =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
var app = builder.Build();

// ===== Pipeline =====
app.UseCors(CorsPolicy); // This applies the "AllowFrontend" policy globally
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();