using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using UMEProje.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Configure Entity Framework Core with In-Memory Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("UMEDb"));

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173") // Vite dev server
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "UME Kalibrasyon API",
        Version = "v1",
        Description = "TUBITAK UME Kalibrasyon ve Laboratuvar Yeterlilik Anket Sistemi"
    });
    
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "UME Kalibrasyon API v1");
});

app.MapGet("/", () => Results.Redirect("/swagger"));

// Apply CORS policy
app.UseCors("AllowFrontend");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Initialize database with sample data (optional)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
    
    // Seed initial LabClients data
    if (!dbContext.LabClients.Any())
    {
        var labClients = new[]
        {
                new UMEProje.Models.LabClient 
                { 
                    CompanyName = "ASELSAN A.Ş.",
                    TaxNumber = "1234567890",
                    ContactEmail = "kalibrasyon@aselsan.com.tr"
                },
                new UMEProje.Models.LabClient 
                { 
                    CompanyName = "ROKETSAN A.Ş.",
                    TaxNumber = "0987654321",
                    ContactEmail = "umekalibrasyonu@roketsan.com.tr"
                },
                new UMEProje.Models.LabClient 
                { 
                    CompanyName = "HAVELSAN A.Ş.",
                    TaxNumber = "1111111111",
                    ContactEmail = "kalibre@havelsan.com.tr"
                }
        };
        
        dbContext.LabClients.AddRange(labClients);
        dbContext.SaveChanges();
    }

    // Seed initial CalibrationSurveys data
    if (!dbContext.CalibrationSurveys.Any())
    {
        var labClients = dbContext.LabClients.ToList();
        
        if (labClients.Count > 0)
        {
            var calibrationSurveys = new[]
            {
                // ASELSAN için anketi
                new UMEProje.Models.CalibrationSurvey
                {
                    DeviceName = "Fluke 87V Dijital Multimetre",
                    LabCategory = "Elektrik Ölçüm",
                    IsApproved = false,
                    Status = "Pending",
                    LabClientId = labClients[0].Id,
                    CreatedAt = DateTime.UtcNow
                },
                new UMEProje.Models.CalibrationSurvey
                {
                    DeviceName = "Basınç Ölçer (0-10 bar)",
                    LabCategory = "Basınç Ölçüm",
                    IsApproved = true,
                    Status = "Approved",
                    LabClientId = labClients[0].Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                
                // ROKETSAN için anketi
                new UMEProje.Models.CalibrationSurvey
                {
                    DeviceName = "Sıcaklık Transmitteri Pt100",
                    LabCategory = "Sıcaklık Ölçüm",
                    IsApproved = false,
                    Status = "Pending",
                    LabClientId = labClients[1].Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new UMEProje.Models.CalibrationSurvey
                {
                    DeviceName = "Akış Ölçer (0-50 m³/h)",
                    LabCategory = "Akış Ölçüm",
                    IsApproved = false,
                    Status = "Pending",
                    LabClientId = labClients[1].Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },

                // HAVELSAN için anketi
                new UMEProje.Models.CalibrationSurvey
                {
                    DeviceName = "Spektrum Analizy Cihazı",
                    LabCategory = "Elektromanyetik Ölçüm",
                    IsApproved = false,
                    Status = "Pending",
                    LabClientId = labClients[2].Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                }
            };
            
            dbContext.CalibrationSurveys.AddRange(calibrationSurveys);
            dbContext.SaveChanges();
        }
    }
}

app.Run();
