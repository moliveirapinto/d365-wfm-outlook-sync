using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WfmOutlookSync.Auth;
using WfmOutlookSync.Configuration;
using WfmOutlookSync.Dataverse;
using WfmOutlookSync.Graph;
using WfmOutlookSync.State;
using WfmOutlookSync.Sync;

var host = new HostBuilder();

host.ConfigureFunctionsWebApplication();

host.ConfigureServices((context, services) =>
{
    var configuration = context.Configuration;

    services
        .AddOptions<DataverseOptions>()
        .Bind(configuration.GetSection(DataverseOptions.SectionName));

    services
        .AddOptions<GraphOptions>()
        .Bind(configuration.GetSection(GraphOptions.SectionName));

    services
        .AddOptions<SyncOptions>()
        .Bind(configuration.GetSection(SyncOptions.SectionName));

    services
        .AddOptions<SyncStateOptions>()
        .Bind(configuration.GetSection(SyncStateOptions.SectionName));

    services.AddSingleton(TimeProvider.System);

    // Dataverse Web API client, authenticated per Dataverse:AuthMode.
    services.AddHttpClient<DataverseWebApiClient>((sp, http) =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataverseOptions>>().Value;
        http.BaseAddress = new Uri(options.WebApiBaseUrl + "/");
    }).AddHttpMessageHandler(sp =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DataverseOptions>>().Value;
        var credential = CredentialFactory.Create(options);
        return new BearerTokenHandler(credential, options.EnvironmentUrl.TrimEnd('/') + "/.default");
    });

    services.AddSingleton<IDataverseShiftRepository, DataverseShiftRepository>();

    // Microsoft Graph client, authenticated per Graph:AuthMode (independent of Dataverse's).
    services.AddSingleton(sp =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GraphOptions>>().Value;
        var credential = CredentialFactory.Create(options);
        return GraphClientFactory.Create(credential);
    });
    services.AddSingleton<IOutlookCalendarService, OutlookCalendarService>();

    // Sync state (Azure Table Storage) - reuses the Functions storage account unless overridden.
    services.AddSingleton(sp =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SyncStateOptions>>().Value;

        TableServiceClient serviceClient = !string.IsNullOrWhiteSpace(options.StorageAccountUri)
            ? new TableServiceClient(new Uri(options.StorageAccountUri), new Azure.Identity.DefaultAzureCredential())
            : new TableServiceClient(configuration.GetValue<string>("AzureWebJobsStorage"));

        var tableClient = serviceClient.GetTableClient(options.TableName);
        tableClient.CreateIfNotExists();
        return tableClient;
    });
    services.AddSingleton<ISyncStateStore, TableSyncStateStore>();

    services.AddSingleton<ShiftSyncEngine>();

    services
        .AddApplicationInsightsTelemetryWorkerService()
        .ConfigureFunctionsApplicationInsights();
});

host.Build().Run();

