using MonkeyCache.SQLite;
using RandomSteamGame.Options;
using RandomSteamGame.Services;

Barrel.ApplicationId = "RandomSteamGame";
Barrel.Current.EmptyExpired();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
{
    builder.Services.Configure<SteamOptions>(
        builder.Configuration.GetSection(nameof(SteamOptions)));

    builder.Services.AddHttpClient<SteamService>();
    builder.Services.AddHttpClient<SteamStoreService>();
}
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
