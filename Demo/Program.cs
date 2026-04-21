using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SemanticKerne.AiProvider.Unified.Models;
using SemanticKerne.AiProvider.Unified.Services;
using SemanticKerne.AiProvider.Unified.Services.Bailian;
using SemanticKerne.AiProvider.Unified.Services.Mcp;
using Serilog;

namespace Demo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 配置 Serilog 文件日志
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    "logs/app-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            builder.Host.UseSerilog();
            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Semantic Kernel Chat API", Version = "v1" });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
            });
            // 注册 SemanticKernelOptions 配置
            builder.Services.Configure<SemanticKernelOptions>(
                builder.Configuration.GetSection("SemanticKernel"));
            
            builder.Services.AddSingleton<ISemanticKernelService, SemanticKernelService>();
            builder.Services.AddSingleton<ISessionManager, SessionManager>();
            builder.Services.AddSingleton<BailianErrorHandler>();

            // 注册 MCP 服务
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<IMcpClientService, McpClientService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var logger = sp.GetRequiredService<ILogger<McpClientService>>();
                return new McpClientService(httpClientFactory, logger, builder.Configuration);
            });
            // 配置 AI 服务的 HttpClient
            builder.Services.AddHttpClient("AIService", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("QW-App/1.0");
                client.MaxResponseContentBufferSize = 1024 * 1024 * 10; // 10MB
            });

            // 配置日志 - 只输出到文件，减少控制台噪音
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger, dispose: true);

            var app = builder.Build();
            // 启用 Swagger (所有环境)
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Semantic Kernel Chat API v1");
            });

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
