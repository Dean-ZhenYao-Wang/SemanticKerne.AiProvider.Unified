namespace SemanticKerne.AiProvider.Unified.Services
{
    public static class ReasoningContextHolder
    {
        private static readonly AsyncLocal<string?> _reasoning = new();
        public static string? Current => _reasoning.Value;
        internal static void Set(string? value) => _reasoning.Value = value;
    }
}