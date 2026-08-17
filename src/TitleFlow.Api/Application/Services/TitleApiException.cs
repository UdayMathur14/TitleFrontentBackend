namespace TitleFlow.Api.Application.Services;

public sealed class TitleConflictException(string message) : Exception(message);
