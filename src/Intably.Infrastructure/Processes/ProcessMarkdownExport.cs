using System.Text;

namespace Intably.Infrastructure.Processes;

internal sealed partial class ProcessService
{
    public async Task<string> ExportMarkdownAsync(
        Guid pirg,
        CancellationToken cancellationToken)
    {
        var process = await RequireProcessAsync(pirg, cancellationToken);
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(Escape(process.Name));
        builder.AppendLine();
        builder.AppendLine($"- **Template:** {Escape(process.TemplateName)} (v{process.TemplateVersion})");
        builder.AppendLine($"- **Status:** {process.Status}");
        builder.AppendLine($"- **Owner:** {Escape(process.OwnerDisplayName)}");
        builder.AppendLine($"- **Created:** {process.CreatedAtUtc:O}");
        if (process.ClosedAtUtc.HasValue)
        {
            builder.AppendLine($"- **Closed:** {process.ClosedAtUtc:O} by {Escape(process.ClosedByDisplayName ?? string.Empty)}");
        }

        builder.AppendLine();
        builder.AppendLine("## Process information");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        foreach (var value in process.InformationValues.OrderBy(value => value.Order))
        {
            builder.Append("| ")
                .Append(Escape(value.Label))
                .Append(" | ")
                .Append(Escape(value.Value))
                .AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine("## Steps");
        foreach (var group in process.StepGroups.OrderBy(group => group.Order))
        {
            builder.AppendLine();
            builder.Append("### ")
                .Append(group.Order)
                .Append(". ")
                .AppendLine(Escape(group.Name));
            builder.AppendLine($"- **Execution mode:** {group.ExecutionMode}");
            if (!string.IsNullOrWhiteSpace(group.Description))
            {
                builder.AppendLine();
                builder.AppendLine(Escape(group.Description));
            }

            foreach (var step in process.Steps
                         .Where(step => step.ProcessStepGroupId == group.Id)
                         .OrderBy(step => step.Order))
            {
                builder.AppendLine();
                builder.Append("#### ")
                    .Append(group.Order)
                    .Append('.')
                    .Append(step.Order)
                    .Append(". ")
                    .AppendLine(Escape(step.Title));
                builder.AppendLine($"- **Status:** {step.Status}");
                builder.AppendLine($"- **Required role:** {Escape(step.RequiredRoleName)}");
                builder.AppendLine($"- **Assignee:** {Escape(step.AssigneeDisplayName ?? "Unassigned")}");
                if (step.DueAtUtc.HasValue)
                {
                    builder.AppendLine($"- **Due:** {step.DueAtUtc:O}");
                }

                if (!string.IsNullOrWhiteSpace(step.Instructions))
                {
                    builder.AppendLine();
                    builder.AppendLine(Escape(step.Instructions));
                }

                if (!string.IsNullOrWhiteSpace(step.ExecutionNote))
                {
                    builder.AppendLine();
                    builder.AppendLine($"**Execution note:** {Escape(step.ExecutionNote)}");
                }

                if (!string.IsNullOrWhiteSpace(step.BlockedReason))
                {
                    builder.AppendLine();
                    builder.AppendLine($"**Blocked reason:** {Escape(step.BlockedReason)}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(process.FinalNote))
        {
            builder.AppendLine();
            builder.AppendLine("## Final note");
            builder.AppendLine();
            builder.AppendLine(Escape(process.FinalNote));
        }

        builder.AppendLine();
        builder.AppendLine("## Timeline");
        foreach (var item in process.AuditEvents
                     .OrderBy(item => item.OccurredAtUtc)
                     .ThenBy(item => item.Id))
        {
            builder.Append("- ")
                .Append(item.OccurredAtUtc.ToString("O"))
                .Append(" — **")
                .Append(Escape(item.Action))
                .Append("** by ")
                .Append(Escape(item.ActorDisplayName))
                .Append(": ")
                .Append(Escape(item.AffectedItem));
            if (!string.IsNullOrWhiteSpace(item.Note))
            {
                builder.Append(" — ").Append(Escape(item.Note));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal)
            .Replace("\r", "<br>", StringComparison.Ordinal);
    }
}
