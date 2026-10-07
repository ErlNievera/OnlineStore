using Storefront.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddHttpClient<CatalogApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:CatalogBaseUrl"]!);
});

builder.Services.AddHttpClient<OrderApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:OrderBaseUrl"]!);
});

builder.Services.AddHttpClient<InventoryApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:InventoryBaseUrl"]!);
});

builder.Services.AddHttpClient<PaymentApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:PaymentBaseUrl"]!);
});

var app = builder.Build();

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