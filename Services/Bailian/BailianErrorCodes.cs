namespace SemanticKerne.AiProvider.Unified.Services.Bailian;

/// <summary>
/// 阿里云百炼API错误码常量定义
/// </summary>
public static class BailianErrorCodes
{
    // 400 系列错误 - 参数错误
    public const string InvalidParameter = "InvalidParameter";
    public const string InvalidRequestError = "invalid_request_error";
    public const string InvalidValue = "invalid_value";
    
    // 思考模式相关
    public const string EnableThinkingMustBeFalse = "parameter.enable_thinking must be set to false for non-streaming calls";
    public const string ThinkingBudgetInvalid = "The thinking_budget parameter must be a positive integer";
    public const string OnlySupportStreamMode = "This model only support stream mode";
    public const string NotSupportEnableSearch = "This model does not support enable_search";
    public const string IncrementalOutputRequired = "The incremental_output parameter must be \"true\" when enable_thinking is true";
    
    // Token 长度相关
    public const string InputLengthOutOfRange = "Range of input length should be [1, xxx]";
    public const string MaxTokensOutOfRange = "Range of max_tokens should be [1, xxx]";
    public const string TotalMessageTokenExceed = "Total message token length exceed model limit";
    
    // 参数范围错误
    public const string TemperatureOutOfRange = "Temperature should be in [0.0, 2.0)";
    public const string TopPOutOfRange = "Range of top_p should be (0.0, 1.0]";
    public const string TopKInvalid = "Parameter top_k be greater than or equal to 0";
    public const string RepetitionPenaltyInvalid = "Repetition_penalty should be greater than 0.0";
    public const string PresencePenaltyOutOfRange = "Presence_penalty should be in [-2.0, 2.0]";
    public const string NOutOfRange = "Range of n should be [1, 4]";
    public const string SeedOutOfRange = "Range of seed should be [0, 9223372036854775807]";
    
    // HTTP 方法错误
    public const string MethodNotSupported = "Request method 'GET' is not supported";
    
    // 工具调用错误
    public const string ToolMessageInvalid = "messages with role \"tool\" must be a response to a preceeding message with \"tool_calls\"";
    public const string ToolNameNotAllowed = "Tool names are not allowed to be [search]";
    public const string ToolChoiceInvalid = "tool_choice is one of the strings that should be [\"none\", \"auto\"]";
    public const string ToolCallNotSupported = "The tool call is not supported";
    
    // 请求格式错误
    public const string RequestBodyInvalid = "Required body invalid, please check the request body format";
    public const string InputMustBeString = "input content must be a string";
    public const string ContentFieldRequired = "The content field is a required field";
    public const string HttpCallNotSupported = "current user api does not support http call";
    public const string MessagesOrPromptRequired = "Either \"prompt\" or \"messages\" must exist and cannot both be none";
    
    // JSON 模式错误
    public const string JsonKeywordRequired = "'messages' must contain the word 'json' in some form";
    public const string JsonModeNotSupportThinking = "Json mode response is not supported when enable_thinking is true";
    
    // 响应格式错误
    public const string ResponseFormatInvalid = "Unknown format of response_format";
    
    // 思考模式限制
    public const string EnableThinkingRestricted = "The value of the enable_thinking parameter is restricted to True";
    
    // 音频相关
    public const string AudioOutputOnlySupportStream = "'audio' output only support with stream=true";
    public const string AudioEmpty = "The audio is empty";
    
    // 文件处理错误
    public const string FileParsingInProgress = "File parsing in progress, please try again later";
    public const string StopParameterInvalid = "The \"stop\" parameter must be of type \"str\", \"list[str]\", \"list[int]\", or \"list[list[int]]\"";
    public const string BatchSizeInvalid = "Value error, batch size is invalid";
    public const string InvalidFileId = "Invalid file";
    public const string MessagesEmpty = "[] is too short";
    
    // 输入验证错误
    public const string MissingParameter = "Required parameter(xxx) missing or invalid";
    public const string FileUrlsRequired = "input must contain file_urls";
    public const string UrlInvalid = "The provided URL does not appear to be valid";
    public const string InputFormatInvalid = "Input should be a valid dictionary or instance of GPT3Message";
    public const string ContentsInvalid = "Value error, contents is neither str nor list of str";
    
    // 文件格式错误
    public const string FileFormatNotSupported = "File format is not supported";
    public const string FileNotFound = "File cannot be found";
    public const string TooManyFiles = "Too many files provided";
    public const string FileSizeExceed = "File exceeds size limit";
    public const string FilePageExceed = "File exceeds page limits";
    public const string FileContentBlank = "File content blank";
    
    // 视频相关错误
    public const string VideoSequenceInvalid = "The video modality input does not meet the requirements because: the range of sequence images shoule be (4, 512)./(4,80)";
    public const string MultimodalFileTooLarge = "Multimodal file size is too large";
    public const string VoiceInvalid = "Input should be 'Cherry', 'Serena', 'Ethan' or 'Chelsie'";
    public const string ImageSizeInvalid = "The image length and width do not meet the model restrictions";
    public const string ImageDecodeFailed = "Failed to decode the image during the data inspection";
    public const string MediaFormatNotSupported = "The media format is not supported or incorrect for the data inspection";
    public const string UserMessageMissing = "The input messages do not contain elements with the role of user";
    public const string MediaDownloadFailed = "Failed to download multimodal content";
    
    // 401 系列错误 - 认证错误
    public const string InvalidApiKey = "InvalidApiKey";
    public const string InvalidApiKeyAlt = "invalid_api_key";
    public const string NotAuthorized = "NOT AUTHORIZED";
    
    // 文档中列出的常见错误
    public const string DataInspectionFailed = "DataInspectionFailed/data_inspection_failed"; // 内容安全检查失败
    public const string Arrearage = "Arrearage"; // 欠费错误
    
    // 403 系列错误 - 权限错误
    public const string AccessDenied = "AccessDenied";
    public const string AccessDeniedAlt = "access_denied";
    public const string AsyncCallNotSupported = "Current user api does not support asynchronous calls";
    public const string SyncCallNotSupported = "current user api does not support synchronous calls";
    public const string PolicyExpired = "Invalid according to Policy: Policy expired";
    public const string AccessDeniedUnpurchased = "AccessDenied.Unpurchased";
    public const string ModelAccessDenied = "Model.AccessDenied";
    public const string AppAccessDenied = "App.AccessDenied";
    public const string WorkspaceAccessDenied = "Workspace.AccessDenied";
    public const string FreeTierExhausted = "AllocationQuota.FreeTierOnly";
    
    // 404 系列错误 - 资源不存在
    public const string ModelNotFound = "ModelNotFound";
    public const string ModelNotFoundAlt = "model_not_found";
    public const string ModelNotSupported = "model_not_supported";
    public const string WorkspaceNotFound = "WorkSpaceNotFound";
    public const string NotFound = "NotFound";
    
    // 409 系列错误 - 冲突
    public const string Conflict = "Conflict";
    public const string ModelInstanceAlreadyExists = "Model instance already exists";
    
    // 429 系列错误 - 限流
    public const string Throttling = "Throttling";
    public const string TooManyFineTuneJobs = "Too many fine-tune job in running";
    public const string TooManyRequestsInRoute = "Too many requests in route";
    public const string RateQuotaExceeded = "Throttling.RateQuota/LimitRequests/limit_requests";
    public const string BurstRateExceeded = "Throttling.BurstRate/limit_burst_rate";
    public const string AllocationQuotaExceeded = "Throttling.AllocationQuota/insufficient_quota";
    public const string BatchRequestsThrottled = "Too many requests. Batch requests are being throttled";
    public const string FreeAllocatedQuotaExceeded = "Free allocated quota exceeded";
    public const string CommodityNotPurchased = "CommodityNotPurchased";
    public const string PrepaidBillOverdue = "PrepaidBillOverdue";
    public const string PostpaidBillOverdue = "PostpaidBillOverdue";
    
    // 400 系列错误 - 文件和输入验证
    public const string InvalidFileResolution = "InvalidFile.Resolution";
    public const string InvalidFileFPS = "InvalidFile.FPS";
    public const string InvalidFileValue = "InvalidFile.Value";
    public const string InvalidFileFrontBody = "InvalidFile.FrontBody";
    public const string InvalidFileFullFace = "InvalidFile.FullFace";
    public const string InvalidFileFaceNotMatch = "InvalidFile.FaceNotMatch";
    public const string InvalidFileContent = "InvalidFile.Content";
    public const string InvalidFileFullBody = "InvalidFile.FullBody";
    public const string InvalidFileBodyPose = "InvalidFile.BodyPose";
    public const string InvalidFileSize = "InvalidFile.Size";
    public const string InvalidFileDuration = "InvalidFile.Duration";
    public const string InvalidFileImageSize = "InvalidFile.ImageSize";
    public const string InvalidFileAspectRatio = "InvalidFile.AspectRatio";
    public const string InvalidFileOpenError = "InvalidFile.Openerror";
    public const string InvalidFileTemplateContent = "InvalidFile.Template.Content";
    public const string InvalidFileFormat = "InvalidFile.Format";
    public const string InvalidFileMultiHuman = "InvalidFile.MultiHuman";
    public const string InvalidPerson = "InvalidPerson";
    public const string InvalidParameterDataInspection = "InvalidParameter.DataInspection";
    public const string FlowNotPublished = "FlowNotPublished";
    public const string InvalidImageSize = "InvalidImage.ImageSize";
    public const string InvalidImageNoHumanFace = "InvalidImage.NoHumanFace";
    public const string InvalidImageResolution = "InvalidImageResolution";
    public const string InvalidImageFormat = "InvalidImageFormat";
    public const string InvalidURL = "InvalidURL";
    public const string AudioTooLong = "The input audio is longer than";
    public const string FileTooLarge = "File size is larger than";
    public const string FileTypeNotSupported = "File type is not supported";
    public const string InvalidImageFileFormat = "InvalidImage.FileFormat";
    public const string URLConnectionRefused = "InvalidURL.ConnectionRefused";
    public const string URLTimeout = "InvalidURL.Timeout";
    public const string BadRequestException = "BadRequestException";
    public const string BadRequestEmptyInput = "BadRequest.EmptyInput";
    public const string BadRequestEmptyParameters = "BadRequest.EmptyParameters";
    public const string BadRequestEmptyModel = "BadRequest.EmptyModel";
    public const string BadRequestIllegalInput = "BadRequest.IllegalInput";
    public const string BadRequestInputDownloadFailed = "BadRequest.InputDownloadFailed";
    public const string BadRequestUnsupportedFileFormat = "BadRequest.UnsupportedFileFormat";
    public const string BadRequestTooLarge = "BadRequest.TooLarge";
    public const string BadRequestResourceNotExist = "BadRequest.ResourceNotExist";
    public const string AllocationQuotaError = "Throttling.AllocationQuota";
    public const string InvalidGarment = "InvalidGarment";
    public const string InvalidSchema = "InvalidSchema";
    public const string InvalidSchemaFormat = "InvalidSchemaFormat";
    public const string AudioShortError = "Audio.AudioShortError";
    public const string AudioSilentError = "Audio.AudioSilentError";
    public const string InvalidInputLength = "InvalidInputLength";
    public const string FaqRuleBlocked = "FaqRuleBlocked";
    public const string ClientDisconnect = "ClientDisconnect";
    public const string ServiceUnavailableError = "ServiceUnavailableError";
    public const string IPInfringementSuspect = "IPInfringementSuspect";
    public const string UnsupportedOperation = "UnsupportedOperation";
    public const string CustomRoleBlocked = "CustomRoleBlocked";
    public const string AudioPreprocessError = "Audio.PreprocessError";
    public const string NoSegmentsMinDuration = "No segments meet minimum duration requirement";
    public const string VoiceNotFound = "BadRequest.VoiceNotFound";
    public const string AudioDecoderError = "Audio.DecoderError";
    public const string AudioRateError = "Audio.AudioRateError";
    public const string AudioDurationLimitError = "Audio.DurationLimitError";
    
    // 500 系列错误 - 服务器内部错误
    public const string InternalError = "InternalError";
    public const string InternalErrorAlt = "internal_error";
    public const string InternalServerError = "Internal server error";
    public const string AudioPreprocessServerError = "audio preprocess server error";
    public const string FileUploadError = "InternalError.FileUpload";
    public const string UploadError = "InternalError.Upload";
    public const string AlgoError = "InternalError.Algo";
    public const string ExpectingDelimiter = "Expecting ',' delimiter";
    public const string MissingContentLength = "Missing Content-Length of multimodal url";
    public const string RequestRejected = "Request rejected by inference engine";
    public const string AlgoExecutionError = "An internal error has occured during algorithm execution";
    public const string InferenceError = "Inference error";
    public const string RoleInvalid = "Role must be in [user, assistant]";
    public const string EmbeddingPipelineError = "Embedding_pipeline_Error";
    public const string BatchingBackendFailed = "Receive batching backend response failed";
    public const string InternalExecutionError = "An internal error has occured during execution";
    public const string ListIndexOutOfRange = "list index out of range";
    public const string InternalTimeoutError = "InternalError.Timeout";
    public const string SystemError = "SystemError";
    public const string ModelServiceFailed = "ModelServiceFailed";
    public const string RequestTimeout = "RequestTimeOut";
    public const string InvokePluginFailed = "InvokePluginFailed";
    public const string AppProcessFailed = "AppProcessFailed";
    public const string RewriteFailed = "RewriteFailed";
    public const string RetrivalFailed = "RetrivalFailed";
    public const string ModelServingError = "ModelServingError";
    public const string ModelUnavailable = "ModelUnavailable";
    
    // SDK 特定错误
    public const string AuthenticationError = "AuthenticationError";
    public const string OpenAIError = "OpenAIError";
    public const string BadURLRequest = "Bad Request for url";
    public const string CannotResolveSymbol = "Cannot resolve symbol 'ttsv2'";
    public const string NetworkError = "NetworkError";
    public const string NoApiKeyException = "NoApiKeyException";
    public const string ConnectException = "ConnectException";
    public const string InputRequiredException = "InputRequiredException";
    public const string MissingPositionalArgument = "missing 1 required positional argument";
    
    // Batch 相关错误
    public const string MismatchedModel = "mismatched_model";
    public const string DuplicateCustomId = "duplicate_custom_id";
    public const string UploadCapacityExceed = "Upload file capacity exceed limit";
    
    // WebSocket 错误
    public const string InvalidPayloadData = "Invalid payload data";
    public const string TextMessageTooBig = "The decoded text message was too big";
    public const string WebSocketTimeout = "TimeoutError: websocket connection could not established";
    public const string UnsupportedAudioFormat = "unsupported audio format";
    public const string InternalUnknownError = "internal unknown error";
    public const string InvalidBackendResponse = "Invalid backend response received";
    public const string NoInputAudioError = "NO_INPUT_AUDIO_ERROR";
    public const string SuccessWithNoValidFragment = "SUCCESS_WITH_NO_VALID_FRAGMENT";
    public const string AsrResponseHaveNoWords = "ASR_RESPONSE_HAVE_NO_WORDS";
    public const string FileDownloadFailed = "FILE_DOWNLOAD_FAILED";
    public const string FileCheckFailed = "FILE_CHECK_FAILED";
    public const string FileTooLargeWS = "FILE_TOO_LARGE";
    public const string FileNormalizeFailed = "FILE_NORMALIZE_FAILED";
    public const string FileParseFailed = "FILE_PARSE_FAILED";
    public const string MkvParseFailed = "MKV_PARSE_FAILED";
    public const string FileTransTaskExpired = "FILE_TRANS_TASK_EXPIRED";
    public const string RequestInvalidFileUrlValue = "REQUEST_INVALID_FILE_URL_VALUE";
    public const string ContentLengthCheckFailed = "CONTENT_LENGTH_CHECK_FAILED";
    public const string File404NotFound = "FILE_404_NOT_FOUND";
    public const string File403Forbidden = "FILE_403_FORBIDDEN";
    public const string FileServerError = "FILE_SERVER_ERROR";
    public const string AudioDurationTooLong = "AUDIO_DURATION_TOO_LONG";
    public const string DecodeError = "DECODE_ERROR";
    public const string ClientError411 = "CLIENT_ERROR-[qwen-tts:]Engine return error code: 411";
    public const string NoValidAudioError = "NO_VALID_AUDIO_ERROR";
    public const string TaskCannotBeNull = "InvalidParameter: task can not be null";
    
    // 其他错误
    public const string WorkspaceNotAuthorised = "BailianGateway.Workspace.NotAuthorised";
    public const string NotSupportEnableThinking = "InvalidParameter.NotSupportEnableThinking";
    public const string VoiceNotSupported = "The requested voice 'xxx' is not supported";
    public const string APIConnectionError = "APIConnectionError";
    public const string InvalidFileDownloadFailed = "InvalidFile.DownloadFailed";
    public const string InvalidFileAudioLengthError = "InvalidFile.AudioLengthError";
    public const string InvalidFileNoHuman = "InvalidFile.NoHuman";
    public const string InvalidFileBodyProportion = "InvalidFile.BodyProportion";
    public const string InvalidFileFacePose = "InvalidFile.FacePose";
}
