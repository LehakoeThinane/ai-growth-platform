namespace AiGrowthPlatform.Business.Application;

public sealed class BusinessNotFoundException(string message) : Exception(message);
