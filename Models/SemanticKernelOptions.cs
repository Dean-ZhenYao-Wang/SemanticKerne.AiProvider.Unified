using Microsoft.Extensions.Configuration;

namespace SemanticKerne.AiProvider.Unified.Models
{
    /// <summary>
    /// Semantic Kernel 配置选项
    /// </summary>
    public class SemanticKernelOptions
    {
        /// <summary>
        /// AI 服务类型（OpenAI, Ollama, DashScope）
        /// </summary>
        public string AiServiceType { get; set; } = "OpenAI";

        /// <summary>
        /// 模型 ID
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// API 端点地址
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// API 密钥
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// HttpClient 超时时间
        /// 默认值：5 分钟
        /// </summary>
        public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 扩展配置数据，用于传递给 AI 服务的额外参数
        /// 支持用户自定义覆盖或追加
        /// </summary>
        public Dictionary<string, object>? ExtensionData { get; set; }

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <param name="errors">收集的错误信息</param>
        /// <returns>配置是否有效</returns>
        public bool Validate(out IList<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(ModelId))
            {
                errors.Add("SemanticKernel:ModelId 配置项不能为空");
            }

            if (string.IsNullOrWhiteSpace(Endpoint))
            {
                errors.Add("SemanticKernel:Endpoint 配置项不能为空");
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                errors.Add("SemanticKernel:ApiKey 配置项不能为空");
            }

            if (HttpClientTimeout <= TimeSpan.Zero)
            {
                errors.Add("SemanticKernel:HttpClientTimeout 必须大于 0");
            }

            if (HttpClientTimeout > TimeSpan.FromHours(24))
            {
                errors.Add("SemanticKernel:HttpClientTimeout 不能超过 24 小时");
            }

            return errors.Count == 0;
        }
    }
}
