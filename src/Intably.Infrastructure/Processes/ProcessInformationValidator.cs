using System.Net.Mail;
using Intably.Application.Processes;
using Intably.Domain.Templates;

namespace Intably.Infrastructure.Processes;

internal static class ProcessInformationValidator
{
    public static void Validate(
        RequestFieldType type,
        bool required,
        string value,
        IEnumerable<string> options)
    {
        if (value.Length > 4000)
        {
            throw new ProcessValidationException(
                "Process information values cannot exceed 4000 characters.");
        }

        if (required && string.IsNullOrWhiteSpace(value))
        {
            throw new ProcessValidationException(
                "Required process information cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (type == RequestFieldType.Email
            && (!MailAddress.TryCreate(value, out var address)
                || !string.Equals(
                    address.Address,
                    value,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new ProcessValidationException(
                "The process information value must be a valid email address.");
        }

        if (type == RequestFieldType.Select
            && !options.Contains(value, StringComparer.Ordinal))
        {
            throw new ProcessValidationException(
                "The process information value must be one of the available options.");
        }
    }
}
