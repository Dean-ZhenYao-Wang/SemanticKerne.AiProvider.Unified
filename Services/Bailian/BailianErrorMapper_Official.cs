using System;
using System.Collections.Concurrent;

namespace SemanticKerne.AiProvider.Unified.Services.Bailian;

/// <summary>
/// 基于阿里云百炼官方文档的错误码映射器
/// 根据 https://help.aliyun.com/zh/model-studio/error-code 文档创建
/// </summary>
public static class BailianErrorMapperOfficial
{
    // 使用并发字典保证线程安全
    private static readonly ConcurrentDictionary<string, BailianErrorMessage> _officialMappings = new();

    static BailianErrorMapperOfficial()
    {
        InitializeOfficialMappings();
    }

    /// <summary>
    /// 初始化基于官方文档的错误码映射
    /// </summary>
    private static void InitializeOfficialMappings()
    {
        // ================= 400 系列错误 =================
        
        // 1. InvalidParameter - 参数错误
        AddMapping("InvalidParameter", 400, "参数错误", 
            "请求参数不符合要求", "请检查请求参数的格式和取值范围");
        
        // 参数.enable_thinking 错误
        AddMapping("parameter.enable_thinking must be set to false for non-streaming calls", 400, 
            "思考模式参数错误", "使用非流式输出方式调用了思考模式模型", 
            "请将enable_thinking参数设置为false，或者改用流式输出方式调用思考模式模型");
        
        // thinking_budget 参数错误
        AddMapping("The thinking_budget parameter must be a positive integer and not greater than xxx", 400,
            "思维链长度参数错误", "thinking_budget参数不在可选值范围内",
            "请参见模型列表中模型的最大思维链长度，指定为大于0且不超过该长度的值");
        
        // 模型仅支持流式输出
        AddMapping("This model only support stream mode", 400,
            "模型仅支持流式输出", "模型仅支持流式输出，但调用时未启用流式输出",
            "请使用流式输出方式调用模型");
        
        // 模型不支持联网搜索
        AddMapping("This model does not support enable_search", 400,
            "模型不支持联网搜索", "当前模型不支持联网搜索能力，但指定了enable_search参数为true",
            "请调用支持联网搜索能力的模型");
        
        // 输入长度超出限制
        AddMapping("Range of input length should be [1, xxx]", 400,
            "输入长度超出限制", "调用模型时输入内容长度超过模型上限",
            "若通过代码调用，请控制messages数组中的Token数在模型最大输入Token范围内；" +
            "使用对话客户端或阿里云百炼控制台进行连续对话时，每次请求都会附带历史记录，容易超出模型限制。超出限制后，请开启新对话");
        
        // temperature 参数错误
        AddMapping("Temperature should be in [0.0, 2.0)", 400,
            "temperature参数错误", "temperature参数设置不在[0.0, 2.0)范围",
            "将temperature参数设置为大于等于0，小于2的数字");
        
        // 2. DataInspectionFailed - 内容安全检查失败
        AddMapping("DataInspectionFailed", 400, "内容安全检查失败",
            "输入或者输出包含疑似敏感内容被绿网拦截", "请修改输入内容后重试");
        
        AddMapping("Input or output data may contain inappropriate content", 400,
            "内容不合规", "输入或者输出包含疑似敏感内容被绿网拦截", "请修改输入内容后重试");
        
        // 3. Arrearage - 欠费错误
        AddMapping("Arrearage", 400, "账户欠费",
            "API Key所属的阿里云账号存在欠费，导致访问被拒绝",
            "前往费用与成本查看是否欠费，未欠费请确认该API Key是否属于当前账号；" +
            "欠费请及时充值。充值后，系统余额可能存在延迟，请稍等后重试", true);
        
        // 4. InvalidFile - 文件无效
        AddMapping("InvalidFile", 400, "文件无效",
            "文件格式、大小、时长、分辨率不符合要求", 
            "请检查文件格式、大小、时长、分辨率是否符合要求");
        
        // 文件格式错误
        AddMapping("File format is not supported", 400, "文件格式不支持",
            "Qwen-Long模型不支持当前文件格式", 
            "Qwen-Long模型仅支持纯文本格式文件(TXT、DOCX、PDF、EPUB、MOBI、MD)");
        
        // 文件大小超限
        AddMapping("File exceeds size limit", 400, "文件大小超限",
            "文件大小超出限制", "确保文件小于150 MB");
        
        // 文件时长错误
        AddMapping("Audio length must be between 1s and 300s", 400, "音频时长不符合要求",
            "音频长度不符合要求", "请确保音频时长在[1, 300]秒范围内");
        
        // ================= 401 系列错误 =================
        
        // InvalidApiKey - API密钥错误
        AddMapping("InvalidApiKey", 401, "API密钥无效",
            "API Key填写错误", 
            "常见错误原因及修正方式：\n" +
            "1. 读取错误的环境变量：错误写法api_key=os.getenv(\"sk-xxx\")，正确写法api_key=os.getenv(\"DASHSCOPE_API_KEY\")\n" +
            "2. 填写错误：阿里云百炼的API Key以sk-开头，请确认未误填其他模型提供商的密钥\n" +
            "3. 地域不匹配：API Key和Base URL属于不同的地域\n" +
            "4. Coding Plan专属API Key：必须以sk-sp-开头，必须配合专属API地址使用", true);
        
        // ================= 403 系列错误 =================
        
        // AccessDenied - 访问被拒绝
        AddMapping("AccessDenied", 403, "访问被拒绝",
            "无权访问此模型", "请前往阿里云百炼控制台申请模型访问权限", true);
        
        // Model access denied
        AddMapping("Model access denied", 403, "模型访问被拒绝",
            "无权限调用对应的标准模型或自定义模型", "请确认模型调用权限配置正确", true);
        
        // Workspace access denied
        AddMapping("Workspace access denied", 403, "工作空间访问被拒绝",
            "无权限访问业务空间的应用或者模型", "请确认账号具有该业务空间的访问权限", true);
        
        // ================= 404 系列错误 =================
        
        // ModelNotFound - 模型不存在
        AddMapping("ModelNotFound", 404, "模型不存在",
            "当前访问的模型不存在，或您还未开通阿里云百炼服务",
            "1. 请对照模型列表中的模型名称，检查您输入的模型名称（参数model的取值）是否正确\n" +
            "2. 请前往模型广场开通模型服务", true);
        
        // ================= 429 系列错误 =================
        
        // Throttling - 请求限流
        AddMapping("Throttling", 429, "请求限流",
            "接口调用触发限流", "请降低调用频率或稍后重试");
        
        // Requests rate limit exceeded
        AddMapping("Requests rate limit exceeded", 429, "请求频率超限",
            "调用频率(RPS/RPM)触发限流", "请参考限流文档，控制调用频率");
        
        // Allocated quota exceeded
        AddMapping("Allocated quota exceeded", 429, "Token配额超限",
            "每秒钟或每分钟消耗Token数(TPS/TPM)触发限流", 
            "前往限流文档查看模型限流条件并调整调用策略");
        
        // ================= 500 系列错误 =================
        
        // InternalError - 内部错误
        AddMapping("InternalError", 500, "内部错误",
            "内部错误", "请稍后重试");
        
        // ModelUnavailable - 模型不可用
        AddMapping("ModelUnavailable", 503, "模型不可用",
            "模型暂时无法提供服务", "请稍后重试");
    }

    /// <summary>
    /// 添加错误映射
    /// </summary>
    private static void AddMapping(string errorCode, int httpStatus, string title, string reason, string solution, bool isCritical = false)
    {
        var category = DetermineCategory(httpStatus, errorCode);
        var message = new BailianErrorMessage
        {
            ErrorCode = errorCode,
            HttpStatus = httpStatus,
            Title = title,
            Reason = reason,
            Solution = solution,
            IsCritical = isCritical,
            Category = category
        };
        
        _officialMappings[errorCode] = message;
    }

    /// <summary>
    /// 根据HTTP状态码和错误码确定错误分类
    /// </summary>
    private static BailianErrorCategory DetermineCategory(int httpStatus, string errorCode)
    {
        return httpStatus switch
        {
            400 => errorCode switch
            {
                var code when code.Contains("File", StringComparison.OrdinalIgnoreCase) || 
                             code.Contains("Audio", StringComparison.OrdinalIgnoreCase) || 
                             code.Contains("Image", StringComparison.OrdinalIgnoreCase) => BailianErrorCategory.FileError,
                var code when code.Contains("Parameter", StringComparison.OrdinalIgnoreCase) || 
                             code.Contains("Invalid", StringComparison.OrdinalIgnoreCase) => BailianErrorCategory.ParameterError,
                var code when code.Contains("Arrearage", StringComparison.OrdinalIgnoreCase) => BailianErrorCategory.QuotaError,
                var code when code.Contains("DataInspection", StringComparison.OrdinalIgnoreCase) => BailianErrorCategory.ContentError,
                _ => BailianErrorCategory.ValidationError
            },
            401 => BailianErrorCategory.AuthenticationError,
            403 => errorCode switch
            {
                var code when code.Contains("Quota", StringComparison.OrdinalIgnoreCase) || 
                             code.Contains("Purchased", StringComparison.OrdinalIgnoreCase) || 
                             code.Contains("Overdue", StringComparison.OrdinalIgnoreCase) => BailianErrorCategory.QuotaError,
                _ => BailianErrorCategory.PermissionError
            },
            404 => BailianErrorCategory.NotFoundError,
            429 => BailianErrorCategory.RateLimitError,
            500 or 503 => BailianErrorCategory.ServerError,
            _ => BailianErrorCategory.Other
        };
    }

    /// <summary>
    /// 根据错误码获取友好的错误消息
    /// </summary>
    public static BailianErrorMessage? GetErrorMessage(string errorCode, string? originalMessage = null)
    {
        if (_officialMappings.TryGetValue(errorCode, out var message))
        {
            // 创建副本
            return new BailianErrorMessage
            {
                ErrorCode = message.ErrorCode,
                HttpStatus = message.HttpStatus,
                Title = message.Title,
                Reason = message.Reason,
                Solution = message.Solution,
                OriginalMessage = originalMessage ?? message.OriginalMessage,
                IsCritical = message.IsCritical,
                Category = message.Category
            };
        }
        
        return null;
    }

    /// <summary>
    /// 获取所有已映射的错误码列表
    /// </summary>
    public static List<string> GetAllErrorCodes()
    {
        return _officialMappings.Keys.ToList();
    }

    /// <summary>
    /// 根据HTTP状态码和错误消息推断错误码
    /// </summary>
    public static string InferErrorCode(int httpStatus, string errorMessage)
    {
        return httpStatus switch
        {
            400 => Infer400ErrorCode(errorMessage),
            401 => "InvalidApiKey",
            403 => Infer403ErrorCode(errorMessage),
            404 => "ModelNotFound",
            429 => "Throttling",
            500 or 503 => "InternalError",
            _ => "UnknownError"
        };
    }

    private static string Infer400ErrorCode(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return "InvalidParameter";
            
        if (errorMessage.Contains("enable_thinking", StringComparison.OrdinalIgnoreCase))
            return "parameter.enable_thinking must be set to false for non-streaming calls";
        if (errorMessage.Contains("thinking_budget", StringComparison.OrdinalIgnoreCase))
            return "The thinking_budget parameter must be a positive integer and not greater than xxx";
        if (errorMessage.Contains("stream mode", StringComparison.OrdinalIgnoreCase))
            return "This model only support stream mode";
        if (errorMessage.Contains("enable_search", StringComparison.OrdinalIgnoreCase))
            return "This model does not support enable_search";
        if (errorMessage.Contains("Range of input length", StringComparison.OrdinalIgnoreCase))
            return "Range of input length should be [1, xxx]";
        if (errorMessage.Contains("temperature", StringComparison.OrdinalIgnoreCase))
            return "Temperature should be in [0.0, 2.0)";
        if (errorMessage.Contains("DataInspection", StringComparison.OrdinalIgnoreCase))
            return "DataInspectionFailed";
        if (errorMessage.Contains("Arrearage", StringComparison.OrdinalIgnoreCase))
            return "Arrearage";
        if (errorMessage.Contains("file", StringComparison.OrdinalIgnoreCase))
            return "InvalidFile";
            
        return "InvalidParameter";
    }

    private static string Infer403ErrorCode(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return "AccessDenied";
            
        if (errorMessage.Contains("Model access denied", StringComparison.OrdinalIgnoreCase))
            return "Model access denied";
        if (errorMessage.Contains("Workspace access denied", StringComparison.OrdinalIgnoreCase))
            return "Workspace access denied";
            
        return "AccessDenied";
    }
}