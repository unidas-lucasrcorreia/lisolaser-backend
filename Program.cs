using LisoLaser.Backend.Infrastructure.DependencyInjection;
using LisoLaser.Backend.Infrastructure.Errors;

var builder = WebApplication.CreateBuilder(args);

// ===== CORS =====
const string CorsPolicy = "AllowFrontend";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicy, p =>
        p.WithOrigins("http://localhost:4200", "https://localhost:4200", "https://lisolaser-cjdtfvargxc4hwgk.brazilsouth-01.azurewebsites.net")
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
app.UseCors(CorsPolicy);
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
