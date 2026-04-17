using System.Collections.Concurrent;

namespace SemanticKerne.AiProvider.Unified.Services.Bailian;

/// <summary>
/// 阿里云百炼错误码映射器
/// 将错误码映射为用户友好的中文错误消息
/// </summary>
public static class BailianErrorMapper
{
    // 使用并发字典保证线程安全
    private static readonly ConcurrentDictionary<string, BailianErrorMessage> _errorMappings = new();
    
    static BailianErrorMapper()
    {
        InitializeErrorMappings();
    }
    
    /// <summary>
    /// 初始化错误码映射(基于官方文档)
    /// </summary>
    private static void InitializeErrorMappings()
    {
        // 400 系列错误 - 参数错误
        AddMapping(global::SemanticKerne.AiProvider.Unified.Services.Bailian.BailianErrorCodes.InvalidParameter, 400, "参数错误", 
            "请求参数不符合要求", "请检查请求参数的格式和取值范围");
        
        AddMapping(global::SemanticKerne.AiProvider.Unified.Services.Bailian.BailianErrorCodes.EnableThinkingMustBeFalse, 400, "思考模式参数错误",
            "使用非流式输出方式调用了思考模式模型", "请将enable_thinking参数设置为false,或者改用流式输出方式调用思考模式模型");
        
        AddMapping(global::SemanticKerne.AiProvider.Unified.Services.Bailian.BailianErrorCodes.ThinkingBudgetInvalid, 400, "思维链长度参数错误",
            "thinking_budget 参数不在可选值范围内", "请参见模型列表中模型的最大思维链长度,指定为大于0且不超过该长度的值");
        
        AddMapping(global::SemanticKerne.AiProvider.Unified.Services.Bailian.BailianErrorCodes.OnlySupportStreamMode, 400, "模型仅支持流式输出",
            "模型仅支持流式输出,但调用时未启用流式输出", "请使用流式输出方式调用模型");
        
        AddMapping(BailianErrorCodes.NotSupportEnableSearch, 400, "模型不支持联网搜索",
            "当前模型不支持联网搜索能力,但指定了enable_search参数为true", "请调用支持联网搜索能力的模型");
        
        AddMapping(BailianErrorCodes.IncrementalOutputRequired, 400, "增量输出参数错误",
            "模型开启思考模式时仅支持增量流式输出,未将incremental_output参数设置为true", "请将incremental_output参数设置为true再调用,API将返回增量内容");
        
        AddMapping(BailianErrorCodes.InputLengthOutOfRange, 400, "输入长度超出限制",
            "调用模型时输入内容长度超过模型上限", "请控制messages数组中的Token数在模型最大输入Token范围内");
        
        AddMapping(BailianErrorCodes.MaxTokensOutOfRange, 400, "max_tokens参数错误",
            "max_tokens参数设置未在[1, 模型最大输出Token数]的范围内", "max_tokens上限请参考模型列表文档中的\"最大输出Token数\"");
        
        AddMapping(BailianErrorCodes.TemperatureOutOfRange, 400, "temperature参数错误",
            "temperature参数设置不在[0.0, 2.0)范围", "将temperature参数设置为大于等于0,小于2的数字");
        
        AddMapping(BailianErrorCodes.TopPOutOfRange, 400, "top_p参数错误",
            "top_p参数设置不在(0.0, 1.0]范围", "将top_p参数设置为大于0,小于等于1的数字");
        
        AddMapping(BailianErrorCodes.TopKInvalid, 400, "top_k参数错误",
            "top_k参数设置为小于0的数字", "将top_k参数设置为大于等于0的数字");
        
        AddMapping(BailianErrorCodes.RepetitionPenaltyInvalid, 400, "repetition_penalty参数错误",
            "repetition_penalty参数设置为小于等于0的数字", "将repetition_penalty参数设置为大于0的数字");
        
        AddMapping(BailianErrorCodes.PresencePenaltyOutOfRange, 400, "presence_penalty参数错误",
            "presence_penalty参数不在[-2.0, 2.0]区间", "将presence_penalty参数设置在[-2.0, 2.0]区间");
        
        AddMapping(BailianErrorCodes.NOutOfRange, 400, "n参数错误",
            "n参数设置未在[1, 4]的范围内", "将n参数设置在[1, 4]范围内");
        
        AddMapping(BailianErrorCodes.SeedOutOfRange, 400, "seed参数错误",
            "seed参数设置未在[0, 9223372036854775807]的范围内", "将seed参数设置在[0, 9223372036854775807]的范围内");
        
        AddMapping(BailianErrorCodes.MethodNotSupported, 400, "HTTP方法不支持",
            "当前接口不支持GET请求方法", "请查阅接口文档,使用该接口支持的请求方法(如POST等)重新发起请求");
        
        AddMapping(BailianErrorCodes.ToolMessageInvalid, 400, "工具调用消息格式错误",
            "在工具调用时没有向messages数组添加Assistant Message", "请将模型第一轮响应的Assistant Message添加到messages数组后再添加Tool Message");
        
        AddMapping(BailianErrorCodes.ToolNameNotAllowed, 400, "工具名称不允许",
            "工具名称无法设置为search", "工具名称请设置为search之外的值");
        
        AddMapping(BailianErrorCodes.ToolChoiceInvalid, 400, "tool_choice参数错误",
            "发起Function Calling时指定的tool_choice参数有误", "请指定为\"auto\"(由大模型自主选择工具)或\"none\"(强制不使用工具)");
        
        AddMapping(BailianErrorCodes.ToolCallNotSupported, 400, "工具调用不支持",
            "使用的模型不支持传入tools参数", "请更换为支持Function Calling的Qwen或DeepSeek模型");
        
        AddMapping(BailianErrorCodes.RequestBodyInvalid, 400, "请求体格式错误",
            "请求体(body)格式不符合接口要求", "请检查请求体,确保为标准的JSON字符串");
        
        AddMapping(BailianErrorCodes.InputMustBeString, 400, "输入内容格式错误",
            "纯文本模型不支持将messages中的content设置为非字符串类型", "请勿将content设置为数组类型");
        
        AddMapping(BailianErrorCodes.ContentFieldRequired, 400, "缺少content字段",
            "发起请求时,未指定content参数", "请指定content参数");
        
        AddMapping(BailianErrorCodes.HttpCallNotSupported, 400, "模型不支持非流式输出",
            "当前模型不支持非流式输出", "请使用流式输出");
        
        AddMapping(BailianErrorCodes.MessagesOrPromptRequired, 400, "缺少消息参数",
            "调用大模型时,既未指定messages参数,也未指定prompt参数", "请指定messages参数");
        
        AddMapping(BailianErrorCodes.JsonKeywordRequired, 400, "JSON模式缺少关键词",
            "使用结构化输出时,提示词中不包含json关键词", "在提示词中加入json(不区分大小写),如:\"请以json格式输出\"");
        
        AddMapping(BailianErrorCodes.JsonModeNotSupportThinking, 400, "JSON模式不支持思考模式",
            "使用结构化输出时开启了模型的思考模式", "请在使用结构化输出时,将enable_thinking设为false关闭思考模式");
        
        AddMapping(BailianErrorCodes.ResponseFormatInvalid, 400, "响应格式参数错误",
            "指定的response_format参数不符合规定", "如需使用结构化输出功能,请将response_format参数设置为{\"type\": \"json_object\"}");
        
        AddMapping(BailianErrorCodes.EnableThinkingRestricted, 400, "思考模式参数受限",
            "部分模型不可将enable_thinking参数设为false", "若通过代码调用,请将enable_thinking设为true");
        
        AddMapping(BailianErrorCodes.AudioOutputOnlySupportStream, 400, "音频输出需要流式",
            "在使用Qwen-Omni模型时,未使用流式输出方式", "设置stream参数为true以启用流式输出");
        
        AddMapping(BailianErrorCodes.AudioEmpty, 400, "音频为空",
            "输入音频时间过短,导致采样点不足", "请增加音频的时间");
        
        AddMapping(BailianErrorCodes.FileParsingInProgress, 400, "文件解析中",
            "使用Qwen-Long模型时,文件未完成解析", "请等待文件解析完成后再重试");
        
        AddMapping(BailianErrorCodes.StopParameterInvalid, 400, "stop参数格式错误",
            "stop参数不符合str, list[str], list[int], 或list[list[int]]格式", "参见千问API文档,设置正确格式的stop参数");
        
        AddMapping(BailianErrorCodes.BatchSizeInvalid, 400, "批次大小无效",
            "调用Embedding模型时,文本数量超过模型上限", "参考Embedding文档中模型的批次大小信息,控制传入文本的数量");
        
        AddMapping(BailianErrorCodes.InvalidFileId, 400, "文件ID无效",
            "提供的file-id无效", "通过OpenAI兼容-File确认file-id是否有效,或重新上传文件获取新的file_id");
        
        AddMapping(BailianErrorCodes.MessagesEmpty, 400, "消息数组为空",
            "输入的messages为空数组", "请添加message后再发起请求");
        
        AddMapping(BailianErrorCodes.MissingParameter, 400, "缺少参数",
            "接口调用参数不合法", "请检查请求参数,确保所有必需参数都已提供且格式正确");
        
        AddMapping(BailianErrorCodes.FileUrlsRequired, 400, "缺少文件URL",
            "使用语音识别的录音文件识别时,未对请求参数file_urls赋值", "请在请求中包含file_urls参数并为其赋值");
        
        AddMapping(BailianErrorCodes.UrlInvalid, 400, "URL无效",
            "传入数据的URL或本地路径无效或不符合要求", "传入URL需要以http://、https://、data:开头;传入本地路径需要以file://开头");
        
        AddMapping(BailianErrorCodes.InputFormatInvalid, 400, "输入格式错误",
            "messages字段的构造格式不符合要求", "请检查messages字段的JSON结构是否正确");
        
        AddMapping(BailianErrorCodes.ContentsInvalid, 400, "输入内容格式错误",
            "使用Embedding模型时,输入不是字符串也不是字符串数组", "请修改输入格式为字符串或字符串列表");
        
        AddMapping(BailianErrorCodes.FileFormatNotSupported, 400, "文件格式不支持",
            "Qwen-Long模型不支持当前文件格式", "Qwen-Long模型仅支持纯文本格式文件(TXT、DOCX、PDF、EPUB、MOBI、MD)");
        
        AddMapping(BailianErrorCodes.FileNotFound, 400, "文件未找到",
            "文件不存在或已删除", "请确认文件是否存在");
        
        AddMapping(BailianErrorCodes.TooManyFiles, 400, "文件数量过多",
            "提供的file-id数量超限", "请确保file-id数量小于100");
        
        AddMapping(BailianErrorCodes.FileSizeExceed, 400, "文件大小超限",
            "文件大小超出限制", "确保文件小于150 MB");
        
        AddMapping(BailianErrorCodes.FilePageExceed, 400, "文件页数超限",
            "文件页数超出限制", "确保文件页数少于15000页");
        
        AddMapping(BailianErrorCodes.FileContentBlank, 400, "文件内容为空",
            "文件内容为空", "确保文件内容不为空");
        
        AddMapping(BailianErrorCodes.TotalMessageTokenExceed, 400, "消息总Token超限",
            "输入总长度超过了10,000,000 Token", "请确保message长度符合要求");
        
        AddMapping(BailianErrorCodes.VideoSequenceInvalid, 400, "视频帧数不符合要求",
            "使用千问VL模型以图像列表方式输入视频时,图像数量不符合要求", "Qwen3-VL与Qwen2.5-VL系列模型需传入4-512张图片;其他模型需传入4-80张图片");
        
        AddMapping(BailianErrorCodes.MultimodalFileTooLarge, 400, "多模态文件过大",
            "向多模态模型传入的本地图像或视频超出大小限制", "本地文件Base64编码后单个文件不得超过10 MB;文件URL请参见文档大小限制");
        
        AddMapping(BailianErrorCodes.VoiceInvalid, 400, "音色参数错误",
            "使用Qwen-Omni或Qwen-TTS时voice参数指定错误", "请指定为'Cherry', 'Serena', 'Ethan'或'Chelsie'中的一个");
        
        AddMapping(BailianErrorCodes.ImageSizeInvalid, 400, "图像尺寸不符合要求",
            "传入千问VL模型的图像尺寸不符合模型的要求", "图像的宽度和高度均不小于10像素,且宽高比不应超过200:1或1:200");
        
        AddMapping(BailianErrorCodes.ImageDecodeFailed, 400, "图像解码失败",
            "图像解码失败", "请确认图像是否有损坏,以及图像格式是否符合要求");
        
        AddMapping(BailianErrorCodes.MediaFormatNotSupported, 400, "媒体格式不支持",
            "无法支持的文件格式或文件无法打开", "请确认文件是否损坏、文件扩展名和实际格式是否匹配、文件格式是否支持");
        
        AddMapping(BailianErrorCodes.UserMessageMissing, 400, "缺少用户消息",
            "调用模型时,未向模型传入User Message", "请确保向模型传入User Message");
        
        AddMapping(BailianErrorCodes.MediaDownloadFailed, 400, "媒体资源下载失败",
            "服务端无法下载公网URL指向的媒体文件", "建议使用与模型服务同地域的存储服务,或尝试使用本地文件(Base64编码或文件路径)");
        
        // 401 系列错误 - 认证错误
        AddMapping(BailianErrorCodes.InvalidApiKey, 401, "API Key无效",
            "API Key填写错误", "请检查API Key是否正确,确保以sk-开头且复制时未包含多余空格或换行符", true);
        
        AddMapping(BailianErrorCodes.InvalidApiKeyAlt, 401, "API Key无效",
            "API Key填写错误", "请检查API Key是否正确,确保以sk-开头且复制时未包含多余空格或换行符", true);
        
        AddMapping(BailianErrorCodes.NotAuthorized, 401, "未授权访问",
            "WorkspaceId值无效,或当前账号不是该业务空间的成员", "请确认WorkspaceId值无误且账号已是该业务空间的成员", true);
        
        // 403 系列错误 - 权限错误
        AddMapping(BailianErrorCodes.AccessDenied, 403, "访问被拒绝",
            "无权访问此模型", "请前往阿里云百炼控制台申请模型访问权限", true);
        
        AddMapping(BailianErrorCodes.AccessDeniedAlt, 403, "访问被拒绝",
            "无权访问此模型", "请前往阿里云百炼控制台申请模型访问权限", true);
        
        AddMapping(BailianErrorCodes.AsyncCallNotSupported, 403, "不支持异步调用",
            "接口不支持异步调用", "请移除请求头中的X-DashScope-Async,或将其值设为disable");
        
        AddMapping(BailianErrorCodes.SyncCallNotSupported, 403, "不支持同步调用",
            "接口不支持同步调用", "请在请求头中设置X-DashScope-Async: enable");
        
        AddMapping(BailianErrorCodes.PolicyExpired, 403, "上传凭证已过期",
            "在获取临时公网URL时,文件上传凭证已经过期", "请重新调用文件上传凭证接口生成新凭证");
        
        AddMapping(BailianErrorCodes.AccessDeniedUnpurchased, 403, "未开通服务",
            "未开通阿里云百炼服务", "请前往阿里云百炼控制台开通服务", true);
        
        AddMapping(BailianErrorCodes.ModelAccessDenied, 403, "模型访问被拒绝",
            "无权限调用对应的标准模型或自定义模型", "请确认模型调用权限配置正确", true);
        
        AddMapping(BailianErrorCodes.AppAccessDenied, 403, "应用访问被拒绝",
            "无权限访问应用或者模型", "请检查应用是否已发布,以及API KEY是否正确", true);
        
        AddMapping(BailianErrorCodes.WorkspaceAccessDenied, 403, "工作空间访问被拒绝",
            "无权限访问业务空间的应用或者模型", "请确认账号具有该业务空间的访问权限", true);
        
        AddMapping(BailianErrorCodes.FreeTierExhausted, 403, "免费额度已用完",
            "开启了免费额度用完即停,且免费额度耗尽后发起请求", "如需付费调用,请关闭免费额度用完即停按钮", true);
        
        // 404 系列错误 - 资源不存在
        AddMapping(BailianErrorCodes.ModelNotFound, 404, "模型不存在",
            "当前访问的模型不存在,或还未开通阿里云百炼服务", "请对照模型列表检查模型名称是否正确,或前往模型广场开通模型服务", true);
        
        AddMapping(BailianErrorCodes.ModelNotFoundAlt, 404, "模型不存在",
            "当前访问的模型不存在", "请对照模型列表检查模型名称是否正确", true);
        
        AddMapping(BailianErrorCodes.ModelNotSupported, 404, "模型不支持OpenAI兼容",
            "当前模型不支持以OpenAI兼容方式接入", "请使用DashScope原生方式调用");
        
        AddMapping(BailianErrorCodes.WorkspaceNotFound, 404, "工作空间不存在",
            "工作空间不存在", "请检查工作空间ID是否正确");
        
        AddMapping(BailianErrorCodes.NotFound, 404, "资源不存在",
            "要查询/操作的资源不存在", "请检查资源ID是否正确");
        
        // 409 系列错误 - 冲突
        AddMapping(BailianErrorCodes.Conflict, 409, "资源冲突",
            "已存在重名的部署实例", "为部署的模型指定不同的后缀名");
        
        AddMapping(BailianErrorCodes.ModelInstanceAlreadyExists, 409, "模型实例已存在",
            "已存在重名的部署实例", "为部署的模型指定不同的后缀名");
        
        // 429 系列错误 - 限流
        AddMapping(BailianErrorCodes.Throttling, 429, "请求限流",
            "接口调用触发限流", "请降低调用频率或稍后重试");
        
        AddMapping(BailianErrorCodes.TooManyFineTuneJobs, 429, "微调任务过多",
            "资源的创建触发平台限制", "可以删除不再使用的模型。如需提高并发量,请申请提额");
        
        AddMapping(BailianErrorCodes.TooManyRequestsInRoute, 429, "请求过多",
            "请求过多触发限流", "请稍后重试");
        
        AddMapping(BailianErrorCodes.RateQuotaExceeded, 429, "请求频率超限",
            "调用频率(RPS/RPM)触发限流", "请参考限流文档,控制调用频率");
        
        AddMapping(BailianErrorCodes.BurstRateExceeded, 429, "请求速率骤增",
            "在未达到限流条件时,调用频率骤增,触发系统稳定性保护机制", "建议优化客户端调用逻辑,采用平滑请求策略");
        
        AddMapping(BailianErrorCodes.AllocationQuotaExceeded, 429, "Token配额超限",
            "每秒钟或每分钟消耗Token数(TPS/TPM)触发限流", "前往限流文档查看模型限流条件并调整调用策略");
        
        AddMapping(BailianErrorCodes.BatchRequestsThrottled, 429, "Batch请求限流",
            "Batch请求过多触发限流", "暂时无法处理您的请求,请稍后再进行重试");
        
        AddMapping(BailianErrorCodes.FreeAllocatedQuotaExceeded, 429, "免费额度已到期",
            "免费额度已到期或耗尽,且该模型暂不支持按量计费", "使用其它模型替换");
        
        AddMapping(BailianErrorCodes.CommodityNotPurchased, 429, "未购买商品",
            "业务空间未订购", "请先订购业务空间服务");
        
        AddMapping(BailianErrorCodes.PrepaidBillOverdue, 429, "预付费账单到期",
            "业务空间预付费账单到期", "请及时充值续费");
        
        AddMapping(BailianErrorCodes.PostpaidBillOverdue, 429, "后付费账单到期",
            "模型推理商品已失效", "请及时充值续费");
        
        // 文件和输入验证错误
        AddMapping(BailianErrorCodes.InvalidFileNoHuman, 400, "图片中未检测到人体",
            "输入图片中没有人或未检测到人脸", "请上传单人照");
        
        AddMapping(BailianErrorCodes.InvalidFileBodyProportion, 400, "人物占比不符合要求",
            "上传图片中人物占比不符合要求", "请上传符合人物占比要求的图片");
        
        AddMapping(BailianErrorCodes.InvalidFileFacePose, 400, "面部姿态不符合要求",
            "上传图片中人物面部姿态不符合要求", "请上传面部可见、头部朝向无严重偏移的图片");
        
        AddMapping(BailianErrorCodes.InvalidFileResolution, 400, "分辨率不符合要求",
            "上传图像大小不符合要求", "请确保图片分辨率符合要求");
        
        AddMapping(BailianErrorCodes.InvalidFileSize, 400, "文件大小不符合要求",
            "文件大小不符合要求", "请确保文件大小在限制范围内");
        
        AddMapping(BailianErrorCodes.InvalidFileFormat, 400, "文件格式不符合要求",
            "文件格式不符合要求", "请使用支持的文件格式");
        
        AddMapping(BailianErrorCodes.InvalidImageFormat, 400, "图片格式错误",
            "输入图片格式错误或文件损坏", "请检查文件是否可正常打开和下载");
        
        AddMapping(BailianErrorCodes.InvalidURL, 400, "URL无效",
            "URL无效或缺失", "请提供正确的URL");
        
        AddMapping(BailianErrorCodes.FaqRuleBlocked, 400, "FAQ规则拦截",
            "命中FAQ规则干预模块", "请调整输入内容");
        
        AddMapping(BailianErrorCodes.ClientDisconnect, 400, "客户端断开连接",
            "任务结束前,客户端主动断开了连接", "请检查代码,不要在任务结束前断开和服务端的连接");
        
        AddMapping(BailianErrorCodes.ServiceUnavailableError, 400, "服务不可用",
            "输入内容长度为0或role不正确", "请检查输入内容长度大于0,并确保参数格式符合API文档的要求");
        
        AddMapping(BailianErrorCodes.IPInfringementSuspect, 400, "涉嫌IP侵权",
            "输入数据涉嫌知识产权侵权", "请检查输入,确保不包含引发侵权风险的内容");
        
        AddMapping(BailianErrorCodes.UnsupportedOperation, 400, "操作不支持",
            "关联的对象不支持该操作", "请检查操作对象和操作类型是否匹配");
        
        AddMapping(BailianErrorCodes.CustomRoleBlocked, 400, "自定义规则拦截",
            "请求或响应内容没有通过自定义策略", "请检查内容或调整自定义策略");
        
        // 500 系列错误 - 服务器内部错误
        AddMapping(BailianErrorCodes.InternalError, 500, "内部错误",
            "内部错误", "请稍后重试");
        
        AddMapping(BailianErrorCodes.InternalErrorAlt, 500, "内部错误",
            "内部错误", "请稍后重试");
        
        AddMapping(BailianErrorCodes.InternalServerError, 500, "服务器内部错误",
            "内部算法错误", "请稍后重试");
        
        AddMapping(BailianErrorCodes.FileUploadError, 500, "文件上传失败",
            "文件上传失败", "请检查OSS配置和网络");
        
        AddMapping(BailianErrorCodes.UploadError, 500, "上传失败",
            "生成结果上传失败", "请检查存储配置或稍后重试");
        
        AddMapping(BailianErrorCodes.AlgoError, 500, "算法错误",
            "服务异常", "请先尝试重试,排除偶发情况");
        
        AddMapping(BailianErrorCodes.RequestRejected, 500, "请求被拒绝",
            "模型服务底层服务器出现错误", "请稍后重试");
        
        AddMapping(BailianErrorCodes.AlgoExecutionError, 500, "算法执行错误",
            "算法运行时发生错误", "请稍后重试");
        
        AddMapping(BailianErrorCodes.InferenceError, 500, "推理错误",
            "推理发生错误", "请检查输入的图片文件是否有损坏或检查人物图片的质量");
        
        AddMapping(BailianErrorCodes.InternalTimeoutError, 500, "内部超时错误",
            "异步任务提交后,在3小时内未返回结果", "请检查任务执行情况,或联系技术支持");
        
        AddMapping(BailianErrorCodes.SystemError, 500, "系统错误",
            "系统错误", "请稍后重试");
        
        AddMapping(BailianErrorCodes.ModelServiceFailed, 500, "模型服务失败",
            "模型服务调用失败", "请稍后重试");
        
        AddMapping(BailianErrorCodes.RequestTimeout, 500, "请求超时",
            "调用大模型时请求超时", "通过流式输出方式发起请求");
        
        AddMapping(BailianErrorCodes.InvokePluginFailed, 500, "插件调用失败",
            "插件调用失败", "请检查插件配置和可用性");
        
        AddMapping(BailianErrorCodes.AppProcessFailed, 500, "应用处理失败",
            "应用流程处理失败", "请检查应用配置和流程节点");
        
        AddMapping(BailianErrorCodes.ModelServingError, 500, "模型服务错误",
            "网络资源目前处于饱和状态", "请稍后再进行尝试");
        
        AddMapping(BailianErrorCodes.ModelUnavailable, 503, "模型不可用",
            "模型暂时无法提供服务", "请稍后重试");
        
        // SDK 特定错误
        AddMapping(BailianErrorCodes.AuthenticationError, 401, "认证错误",
            "使用DashScope SDK时未提供API Key", "具体配置API Key的方法,请参见配置API Key到环境变量", true);
        
        AddMapping(BailianErrorCodes.OpenAIError, 401, "OpenAI SDK错误",
            "使用OpenAI SDK时未传入API Key", "请配置API Key", true);
        
        AddMapping(BailianErrorCodes.NetworkError, 500, "网络错误",
            "网络连接异常", "请检查本地网络环境");
        
        AddMapping(BailianErrorCodes.NoApiKeyException, 401, "未找到API Key",
            "环境变量配置没有生效", "您可以重启客户端或IDE后重试", true);
        
        AddMapping(BailianErrorCodes.ConnectException, 500, "连接失败",
            "本地网络环境存在异常", "请检查本地网络,建议您更换网络环境或服务器进行测试");
        
        // 配额错误
        AddMapping(BailianErrorCodes.Arrearage, 400, "账户欠费",
            "API Key所属的阿里云账号存在欠费", "请前往费用与成本查看是否欠费并及时充值", true);
        
        AddMapping(BailianErrorCodes.DataInspectionFailed, 400, "数据检查失败",
            "输入或者输出包含疑似敏感内容被绿网拦截", "请修改输入内容后重试");
        
        AddMapping(BailianErrorCodes.APIConnectionError, 400, "API连接错误",
            "本地网络问题,通常是因为开启了代理", "请关闭或者重启代理");
        
        // 音频错误
        AddMapping(BailianErrorCodes.AudioShortError, 400, "音频时长过短",
            "用于CosyVoice声音复刻的音频有效时长过短", "音频时长应尽量控制在10~15秒之间");
        
        AddMapping(BailianErrorCodes.AudioSilentError, 400, "音频为静音",
            "CosyVoice声音复刻音频文件为静音或非静音长度过短", "请录制包含至少一段超过5秒的连续语音的音频");
        
        AddMapping(BailianErrorCodes.AudioPreprocessError, 400, "音频预处理错误",
            "音频预处理异常", "请参见录音操作指南重新录制音频");
        
        AddMapping(BailianErrorCodes.AudioDecoderError, 400, "音频解码失败",
            "音频文件解码失败", "请检查音频文件是否损坏,并确保音频满足格式要求");
        
        AddMapping(BailianErrorCodes.AudioRateError, 400, "音频采样率不支持",
            "音频采样率不符合要求", "采样率需大于等于24000 Hz");
        
        AddMapping(BailianErrorCodes.AudioDurationLimitError, 400, "音频时长超限",
            "音频时长超过限制", "音频不得超过60秒");
        
        // WebSocket 错误
        AddMapping(BailianErrorCodes.InvalidPayloadData, 400, "WebSocket数据格式错误",
            "发送给服务端的JSON格式有误", "请检查发送的数据格式是否正确");
        
        AddMapping(BailianErrorCodes.WebSocketTimeout, 500, "WebSocket连接超时",
            "无法在5秒内建立websocket连接", "请检查本地网络、防火墙设置,或更换网络环境");
        
        AddMapping(BailianErrorCodes.NoInputAudioError, 400, "未检测到有效语音",
            "未检测到有效语音", "请检查是否有音频输入,以及音频格式是否正确");
        
        AddMapping(BailianErrorCodes.NoValidAudioError, 400, "无效音频",
            "待识别音频无效", "请检查音频格式、采样率等是否满足要求");
        
        AddMapping(BailianErrorCodes.TaskCannotBeNull, 400, "任务参数为空",
            "WebSocket API缺少必要字段", "请检查指令格式是否正确");
        
        // 其他错误
        AddMapping(BailianErrorCodes.WorkspaceNotAuthorised, 200, "工作空间未授权",
            "访问URL中包含了特殊字符或非标准格式", "请重新访问百炼控制台首页,再导航到目标页面");
        
        AddMapping(BailianErrorCodes.NotSupportEnableThinking, 400, "模型不支持思考模式",
            "当前使用的模型不支持设定参数enable_thinking", "请求时去掉enable_thinking参数,或使用支持思考模式的模型");
        
        AddMapping(BailianErrorCodes.VoiceNotSupported, 400, "音色不支持",
            "声音复刻时的模型与语音合成时的模型不一致", "请检查声音复刻时的target_model和语音合成时的model是否一致");
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
        
        _errorMappings[errorCode] = message;
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
                var code when code.Contains("File") || code.Contains("file") || code.Contains("Audio") || code.Contains("audio") || code.Contains("Image") || code.Contains("image") || code.Contains("Video") || code.Contains("video") => BailianErrorCategory.FileError,
                var code when code.Contains("Invalid") || code.Contains("parameter") || code.Contains("Parameter") => BailianErrorCategory.ParameterError,
                var code when code.Contains("Validation") || code.Contains("validation") => BailianErrorCategory.ValidationError,
                var code when code.Contains("Arrearage") => BailianErrorCategory.QuotaError,
                _ => BailianErrorCategory.ParameterError
            },
            401 => BailianErrorCategory.AuthenticationError,
            403 => errorCode switch
            {
                var code when code.Contains("Quota") || code.Contains("Purchased") || code.Contains("Overdue") => BailianErrorCategory.QuotaError,
                _ => BailianErrorCategory.PermissionError
            },
            404 => BailianErrorCategory.NotFoundError,
            409 => BailianErrorCategory.Other,
            429 => BailianErrorCategory.RateLimitError,
            500 or 503 => BailianErrorCategory.ServerError,
            _ => BailianErrorCategory.Other
        };
    }
    
    /// <summary>
    /// 根据错误码获取友好的错误消息
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="originalMessage">原始错误消息(可选)</param>
    /// <returns>友好的错误消息</returns>
    public static BailianErrorMessage? GetErrorMessage(string errorCode, string? originalMessage = null)
    {
        if (_errorMappings.TryGetValue(errorCode, out var message))
        {
            // 创建副本,避免修改静态字典中的对象
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
    /// 根据HTTP状态码和错误消息内容推断错误码
    /// </summary>
    /// <param name="httpStatus">HTTP状态码</param>
    /// <param name="errorMessage">错误消息</param>
    /// <returns>推断的错误码</returns>
    public static string InferErrorCode(int httpStatus, string errorMessage)
    {
        // 根据HTTP状态码和错误消息内容进行推断
        return httpStatus switch
        {
            400 => Infer400ErrorCode(errorMessage),
            401 => BailianErrorCodes.InvalidApiKey,
            403 => Infer403ErrorCode(errorMessage),
            404 => BailianErrorCodes.ModelNotFound,
            429 => Infer429ErrorCode(errorMessage),
            500 or 503 => BailianErrorCodes.InternalError,
            _ => "UnknownError"
        };
    }
    
    private static string Infer400ErrorCode(string errorMessage)
    {
        if (errorMessage.Contains("enable_thinking", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.InvalidParameter;
        if (errorMessage.Contains("token", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.InputLengthOutOfRange;
        if (errorMessage.Contains("temperature", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.TemperatureOutOfRange;
        if (errorMessage.Contains("max_tokens", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.MaxTokensOutOfRange;
        if (errorMessage.Contains("file", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.FileFormatNotSupported;
        if (errorMessage.Contains("audio", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.AudioEmpty;
        
        return BailianErrorCodes.InvalidParameter;
    }
    
    private static string Infer403ErrorCode(string errorMessage)
    {
        if (errorMessage.Contains("quota", StringComparison.OrdinalIgnoreCase) || errorMessage.Contains("limit", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.AllocationQuotaExceeded;
        if (errorMessage.Contains("purchased", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.AccessDeniedUnpurchased;
        
        return BailianErrorCodes.AccessDenied;
    }
    
    private static string Infer429ErrorCode(string errorMessage)
    {
        if (errorMessage.Contains("rate", StringComparison.OrdinalIgnoreCase) || errorMessage.Contains("limit", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.RateQuotaExceeded;
        if (errorMessage.Contains("quota", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.AllocationQuotaExceeded;
        if (errorMessage.Contains("batch", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.BatchRequestsThrottled;
        
        return BailianErrorCodes.Throttling;
    }
}
