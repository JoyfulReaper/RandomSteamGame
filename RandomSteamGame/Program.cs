using MonkeyCache.SQLite;
using RandomSteamGame;
using RandomSteamGame.Options;
using RandomSteamGame.Services;

// MonkeyCache
Barrel.ApplicationId = "RandomSteamGame";
Barrel.Current.EmptyExpired();

// Services
var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddRazorPages();

    builder.Services.AddRandomSteamGame(builder.Configuration);
}
var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.Run();
