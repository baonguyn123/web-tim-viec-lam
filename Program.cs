using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Text;
using web_jobs.Models;
using web_jobs.Repository;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// 🌍 Cấu hình ngôn ngữ
var cultureInfo = new CultureInfo("en-US");
cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

// ⚙️ Kết nối Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 👤 Cấu hình Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
});

// 🔑 JWT CONFIG
var key = builder.Configuration["Jwt:Key"];
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];

// 🧩 Authentication
builder.Services.AddAuthentication(options =>
{
    // Mặc định MVC dùng cookie Identity
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
})

.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };
})
.AddGoogle(googleOptions =>
{
    googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
});

// 💾 Repository & Service
builder.Services.AddScoped<ICategoryRepository, EFCategoryRepository>();
builder.Services.AddScoped<IJobRepository, EFJobRepository>();
builder.Services.AddScoped<IEmployerRepository, EFEmployerRepository>();
builder.Services.AddScoped<IJobTypeRepository, EFJobTypeRepository>();
builder.Services.AddScoped<ICandidateProfileRepository, EFCandidateProfileRepository>();
builder.Services.AddScoped<ISavedJobRepository, EFSavedJobRepository>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddHttpClient();

// 📘 MVC + API + Swagger
builder.Services.AddControllersWithViews();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Job API",
        Version = "v1",
        Description = "API cho ứng dụng việc làm (MVC + Flutter)"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token JWT dạng: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// 🚀 Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Job API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication(); // ✅ Authentication trước Authorization
app.UseAuthorization();

// 👇 Map routes
app.MapRazorPages();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 👑 Seed tài khoản Admin
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    await SeedAdminUserAsync(userManager, roleManager);
}

async Task SeedAdminUserAsync(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
{
    string adminRoleName = "Admin";
    string adminEmail = "Bon@gmail.com";
    string adminPassword = "Bon123456@";

    if (!await roleManager.RoleExistsAsync(adminRoleName))
        await roleManager.CreateAsync(new IdentityRole(adminRoleName));

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new AppUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (!result.Succeeded)
            throw new Exception("Không thể tạo tài khoản admin: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    if (!await userManager.IsInRoleAsync(adminUser, adminRoleName))
        await userManager.AddToRoleAsync(adminUser, adminRoleName);
}

app.Run();
