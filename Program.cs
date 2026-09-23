using EmpactRecipeOnline.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<JsonDataStore>();
builder.Services.AddSingleton<AllergenService>();
builder.Services.AddSingleton<ResalePricingService>();
builder.Services.AddSingleton<WorkspaceService>();
builder.Services.AddHttpClient<NutritionService>(client => client.Timeout = TimeSpan.FromSeconds(12));
builder.Services.AddDataProtection();
builder.Services.AddAuthentication("EmpactCookie")
    .AddCookie("EmpactCookie", options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
