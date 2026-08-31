using Intably.Application.Users;

namespace Intably.Application.Processes;

public interface IProcessService
{
    Task<IReadOnlyCollection<ProcessSummary>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<ProcessDetails> StartAsync(
        StartProcessRequest request,
        CurrentUserProfile actor,
        CancellationToken cancellationToken);

    Task<ProcessDetails> GetAsync(
        Guid pirg,
        CancellationToken cancellationToken);

    Task<ProcessDetails> SetStepStatusAsync(
        Guid pirg,
        Guid psrg,
        SetProcessStepStatusRequest request,
        CurrentUserProfile actor,
        CancellationToken cancellationToken);

    Task<ProcessDetails> AssignStepAsync(
        Guid pirg,
        Guid psrg,
        AssignProcessStepRequest request,
        CurrentUserProfile actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EligibleAssignee>> GetEligibleAssigneesAsync(
        Guid pirg,
        Guid psrg,
        CancellationToken cancellationToken);

    Task<ProcessDetails> CloseAsync(
        Guid pirg,
        CloseProcessRequest request,
        CurrentUserProfile actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProcessTimelineEvent>> GetTimelineAsync(
        Guid pirg,
        CancellationToken cancellationToken);

    Task<string> ExportMarkdownAsync(
        Guid pirg,
        CancellationToken cancellationToken);
}
