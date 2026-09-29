namespace AiGrowthPlatform.Business.Domain;

public sealed class BusinessValidationException(string message) : Exception(message);
