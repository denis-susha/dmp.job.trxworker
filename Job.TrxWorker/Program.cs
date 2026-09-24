using DMP.BL.Models.Configurations;
using DMP.BL.Services;
using DMP.DataAccess;
using Job.TrxWorker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    options.UseUtcTimestamp = true;
});
builder.Logging.SetMinimumLevel(LogLevel.Debug);

var dmpDataSource = BuildDataSource(builder.Configuration.GetConnectionString("DmpConnection"));
var billingDataSource = BuildDataSource(builder.Configuration.GetConnectionString("BillingDbConnection"));
builder.Services.AddDbContextFactory<DmpDbContext>(options => options.UseNpgsql(dmpDataSource));
builder.Services.AddDbContextFactory<BillingDbContext>(options => options.UseNpgsql(billingDataSource));

builder.Services.Configure<BitcartOptions>(builder.Configuration.GetSection(BitcartOptions.Position));
builder.Services.Configure<DmpHostsSettings>(builder.Configuration.GetSection(DmpHostsSettings.Position));
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection(MinioSettings.Position));

builder.Services.AddHttpClient(BitcartOptions.Position, (services, client) =>
    client.BaseAddress = new Uri(services.GetRequiredService<IOptions<BitcartOptions>>().Value.ApiUrl));

builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddTransient<ITransactionService, TransactionService>();
builder.Services.AddHostedService<TransactionWorkerService>();

await builder.Build().RunAsync();

static NpgsqlDataSource BuildDataSource(string? connectionString)
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.EnableDynamicJson(); // jsonb columns
    return dataSourceBuilder.Build();
}
