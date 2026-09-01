namespace Intably.Application.Templates;

public sealed class TemplateNameConflictException(string name)
    : Exception($"A template named '{name.Trim()}' already exists.");
