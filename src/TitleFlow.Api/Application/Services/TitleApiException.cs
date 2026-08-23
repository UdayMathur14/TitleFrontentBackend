namespace TitleFlow.Api.Application.Services;

public sealed class TitleConflictException(string message) : Exception(message);

public sealed class PublicationTitleConflictException(string message) : Exception(message);
