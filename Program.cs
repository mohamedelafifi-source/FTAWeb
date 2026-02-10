var builder = WebApplication.CreateBuilder(args);

// Load optional secrets file (add appsettings.Secrets.json with your connection string; it is gitignored)
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: false);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSession(options => { options.IdleTimeout = TimeSpan.FromHours(2); });

// Family storage: use Azure Blob when ConnectionString or (AccountName + AccountKey) is set
var azureSection = builder.Configuration.GetSection("FamilyStorage").GetSection("Azure");
var conn = builder.Configuration["FamilyStorage:Azure:ConnectionString"]?.Trim()
    ?? azureSection["ConnectionString"]?.Trim()
    ?? Environment.GetEnvironmentVariable("FamilyStorage__Azure__ConnectionString")?.Trim();
var hasAccount = !string.IsNullOrWhiteSpace(azureSection["AccountName"]?.Trim()) && !string.IsNullOrWhiteSpace(azureSection["AccountKey"]?.Trim());
var useAzure = !string.IsNullOrWhiteSpace(conn) || hasAccount;
if (useAzure)
    builder.Services.AddScoped<FTAWeb.Services.IFamilyStorageService, FTAWeb.Services.AzureBlobFamilyStorageService>();
else
    builder.Services.AddScoped<FTAWeb.Services.IFamilyStorageService, FTAWeb.Services.FamilyStorageService>();

builder.Services.AddScoped<FTAWeb.Services.IFamilyPasswordService, FTAWeb.Services.FamilyPasswordService>();
builder.Services.AddSingleton<FTAWeb.Services.IStorageModeService, FTAWeb.Services.StorageModeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
