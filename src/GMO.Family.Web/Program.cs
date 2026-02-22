using GMO.Family.Web.Configuration;
using GMO.Family.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Load env vars from Process, User, and Machine (Windows System) so Configuration["VAR"] picks them up everywhere.
builder.Configuration.Sources.Add(new AllScopesEnvironmentVariablesConfigurationSource());

// Add services to the container.
builder.Services.AddControllersWithViews();

var googleAuthEnabled = builder.Services.AddGoogleAuthentication(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

if (googleAuthEnabled)
    app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
