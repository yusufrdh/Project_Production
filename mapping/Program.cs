using KP_InternalSystem.Data; // Pastikan namespace ini ada
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Registrasi Database
builder.Services.AddDbContext<RatDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost;Database=db_production;Trusted_Connection=True;TrustServerCertificate=True;"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// --- KEMBALI KE SETTINGAN AWAL (PIT MAPPING) ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=PitUi}/{action=Index}/{id?}"); // Default tetap PitUi

app.Run();