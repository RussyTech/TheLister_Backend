using System.Text;
using System.Text.Json.Serialization;
using API.Data;
using API.Entities;
using API.RequestHelper;
using API.Services;
using API.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ─── MVC ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ─── EF Core (SQLite) ─────────────────────────────────────────────────────────
builder.Services.AddDbContext<StoreContext>(opt =>
{
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    opt.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// ─── Identity ─────────────────────────────────────────────────────────────────
builder.Services.AddIdentityCore<ApplicationUser>(opt =>
{
    opt.User.RequireUniqueEmail     = true;
    opt.SignIn.RequireConfirmedEmail = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<StoreContext>()
.AddSignInManager<SignInManager<ApplicationUser>>()
.AddDefaultTokenProviders();

// ─── JWT ──────────────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["JwtSettings:TokenKey"]
            ?? "your-super-secret-jwt-key-at-least-32-characters-long";

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer           = true,
            ValidIssuer              = "syncpilot-api",
            ValidateAudience         = true,
            ValidAudience            = "syncpilot-client",
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromMinutes(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Headers["Authorization"]
                    .FirstOrDefault()?.Split(' ').Last();
                if (string.IsNullOrEmpty(token))
                    token = context.Request.Cookies["token"];
                if (!string.IsNullOrEmpty(token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

// ─── Authorization ────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

// ─── HTTP Client (for eBay + Amazon API calls) ────────────────────────────────
builder.Services.AddHttpClient();

// ─── Memory Cache (for eBay category trees, price snapshots) ─────────────────
builder.Services.AddMemoryCache();

// ─── App Services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAmazonProductService, AmazonProductService>();
builder.Services.AddScoped<IEbayListingService, EbayListingService>();
builder.Services.AddScoped<IEbayAuthService, EbayAuthService>();
builder.Services.AddScoped<IEbayInventoryService, EbayInventoryService>();
builder.Services.AddScoped<IEbayPolicyService, EbayPolicyService>();
// builder.Services.AddScoped<IEbayImageService, EbayImageService>();

builder.Services.AddTransient<StripContentLanguageHandler>();
builder.Services.AddHttpClient(string.Empty)
    .AddHttpMessageHandler<StripContentLanguageHandler>();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<MappingProfiles>();
    // cfg.AddProfile<AnotherProfile>();
});



// ─── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors(opt =>
{
    opt.AllowAnyHeader()
       .AllowAnyMethod()
       .AllowCredentials()
       .WithOrigins(
           "http://localhost:3000",
           "https://localhost:3000",
           "http://localhost:5173",
           "https://localhost:5173"
       );
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await DbInitialiser.InitDb(app);

app.Run();