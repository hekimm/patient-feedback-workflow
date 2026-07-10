using HastaGeriBildirim.Data;
using HastaGeriBildirim.Repositories;
using HastaGeriBildirim.Services;
using HastaGeriBildirim.Services.Integrations;

namespace HastaGeriBildirim.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHgbDataAccess(this IServiceCollection services)
    {
        services.AddSingleton<OracleConnectionFactory>();

        services.AddScoped<DashboardRepository>();
        services.AddScoped<SurveyRepository>();
        services.AddScoped<FeedbackRepository>();
        services.AddScoped<ServiceRecoveryRepository>();
        services.AddScoped<AlertRepository>();
        services.AddScoped<ClinicalEventRepository>();
        services.AddScoped<SurveyTemplateRepository>();
        services.AddScoped<ChannelRepository>();
        services.AddScoped<ComplianceRepository>();
        services.AddScoped<AuditLogRepository>();
        services.AddScoped<UserRepository>();
        services.AddScoped<IntegrationLogRepository>();
        services.AddScoped<DispatchRepository>();
        services.AddScoped<TriggerRuleRepository>();
        services.AddScoped<SettingsRepository>();
        services.AddScoped<KpiRepository>();
        services.AddScoped<ReportExportRepository>();
        services.AddScoped<MaintenanceRepository>();
        services.AddScoped<SentimentRepository>();
        services.AddScoped<WebhookReplayRepository>();
        services.AddScoped<UserScopeRepository>();

        return services;
    }

    public static IServiceCollection AddHgbApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<SurveyFlowService>();
        services.AddScoped<SurveyDispatchService>();
        services.AddScoped<ServiceRecoveryService>();
        services.AddScoped<AuditService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<AuthService>();
        services.AddScoped<TokenService>();
        services.AddScoped<ReportExportService>();
        services.AddScoped<MaintenanceService>();
        services.AddScoped<SentimentService>();
        services.AddScoped<WhatsAppChatSurveyService>();
        services.AddScoped<WebhookSecurityService>();
        services.AddSingleton<IPiiCryptoService, PiiCryptoService>();
        services.AddScoped<IClinicalEventIngestionService, ClinicalEventIngestionService>();
        services.AddScoped<ISentimentAnalyzer, LocalLexiconSentimentAnalyzer>();
        services.AddSingleton<ProductionReadinessValidator>();

        services.AddHostedService<ProductionReadinessHostedService>();
        services.AddHostedService<PiiMigrationHostedService>();
        services.AddHostedService<HgbBackgroundService>();

        return services;
    }

    public static IServiceCollection AddHgbIntegrationClients(this IServiceCollection services)
    {
        services.AddHttpClient<ProbelSmsGatewayClient>();
        services.AddTransient<ISurveyChannelClient>(sp => sp.GetRequiredService<ProbelSmsGatewayClient>());

        services.AddHttpClient<WhatsAppSurveyClient>();
        services.AddTransient<IWhatsAppSurveyClient>(sp => sp.GetRequiredService<WhatsAppSurveyClient>());
        services.AddTransient<ISurveyChannelClient>(sp => sp.GetRequiredService<WhatsAppSurveyClient>());

        services.AddHttpClient<IBiExportClient, ProbelBiExportClient>();

        return services;
    }
}
