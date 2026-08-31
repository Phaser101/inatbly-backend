namespace Intably.Application.Templates;

public interface ITemplateService
{
    Task<IReadOnlyCollection<TemplateSummary>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<TemplateDetails?> GetAsync(
        Guid ptrg,
        CancellationToken cancellationToken);

    Task<TemplateDetails?> GetPublishedAsync(
        Guid ptrg,
        CancellationToken cancellationToken);

    Task<TemplateDetails> CreateAsync(
        SaveTemplateRequest request,
        Guid ownerGrg,
        CancellationToken cancellationToken);

    Task<TemplateDetails?> UpdateAsync(
        Guid ptrg,
        SaveTemplateRequest request,
        CancellationToken cancellationToken);

    Task<TemplateDetails?> PublishAsync(
        Guid ptrg,
        CancellationToken cancellationToken);

    Task<TemplateDetails?> DuplicateAsync(
        Guid ptrg,
        Guid ownerGrg,
        CancellationToken cancellationToken);

    Task<bool> ArchiveAsync(Guid ptrg, CancellationToken cancellationToken);
}
