using Microsoft.Extensions.Options;
using SemanticKerne.AiProvider.Unified.Models;
using SemanticKerne.AiProvider.Unified.Services;
using SemanticKerne.AiProvider.Unified.Services.Bailian;
using SemanticKerne.AiProvider.Unified.Services.Mcp;

namespace Demo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();

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

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
