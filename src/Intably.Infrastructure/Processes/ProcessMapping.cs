using Intably.Application.Processes;
using Intably.Domain.Processes;

namespace Intably.Infrastructure.Processes;

internal sealed partial class ProcessService
{
    private static ProcessDetails MapDetails(ProcessInstance process)
    {
        return new ProcessDetails(
            process.Id,
            process.TemplateId,
            process.Name,
            process.TemplateName,
            process.TemplateVersion,
            process.RequireSequentialSteps,
            process.Status.ToString(),
            process.Context,
            process.OwnerUserId,
            process.OwnerDisplayName,
            process.CreatedAtUtc,
            process.ClosedAtUtc,
            process.ClosedByUserId,
            process.ClosedByDisplayName,
            process.FinalNote,
            Convert.ToBase64String(process.RowVersion),
            process.RequestValues
                .OrderBy(value => value.Order)
                .Select(value => new ProcessRequestValueDetails(
                    value.SourceRequestFieldId,
                    value.Label,
                    value.FieldType,
                    value.IsRequired,
                    value.Value))
                .ToArray(),
            process.Steps
                .OrderBy(step => step.Order)
                .Select(step => MapStep(process, step))
                .ToArray());
    }

    private static ProcessStepDetails MapStep(
        ProcessInstance process,
        ProcessStep step)
    {
        return new ProcessStepDetails(
            step.Id,
            step.SourceTemplateStepId,
            step.Order,
            step.Title,
            step.RequiredRoleId,
            step.RequiredRoleName,
            step.Instructions,
            step.SupportingUrl,
            step.AssigneeUserId,
            step.AssigneeDisplayName,
            step.Status.ToString(),
            step.DueAtUtc,
            step.NoteRequired,
            step.ExecutorUserId,
            step.ExecutorDisplayName,
            step.CompletedAtUtc,
            step.ExecutionNote,
            step.BlockedReason,
            process.CanUpdateStep(step.Id),
            Convert.ToBase64String(step.RowVersion));
    }
}
