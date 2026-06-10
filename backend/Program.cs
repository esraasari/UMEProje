using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using UMEProje.Data;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
// QuestPDF License Setup (Development Mode)
QuestPDF.Settings.License = LicenseType.Community;

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

// ==========================================
// JWT KİMLİK DOĞRULAMA (AUTHENTICATION) EKLENDİ
// ==========================================
var jwtKey = Encoding.ASCII.GetBytes("TubitakUmeSuperSecretKey1234567890!"); 
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// ==========================================
// SWAGGER GÜVENLİK AYARLARI EKLENDİ
// ==========================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UME Kalibrasyon API",
        Version = "v1",
        Description = "TUBITAK UME Kalibrasyon ve Laboratuvar Yeterlilik Anket Sistemi"
    });
    
    // Swagger'a Kilit (Authorize) İkonu Ekleme
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Kimlik Doğrulama. Örnek format: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
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

app.UseAuthentication();

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
