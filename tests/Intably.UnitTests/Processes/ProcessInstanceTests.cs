using Intably.Domain.Processes;

namespace Intably.UnitTests.Processes;

public sealed class ProcessInstanceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SetStepStatus_WithoutBlockedReason_Throws()
    {
        var process = CreateProcessWithStep();
        var step = process.Steps.Single();

        var action = () =>
            process.SetStepStatus(
                step.Id,
                ProcessStepStatus.Blocked,
                Guid.NewGuid(),
                "Process User",
                null,
                Now);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("A blocked step requires a reason.", exception.Message);
    }

    [Fact]
    public void Close_WithIncompleteStep_Throws()
    {
        var process = CreateProcessWithStep();

        var action = () =>
            process.Close(Guid.NewGuid(), "Process User", "Done", Now);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal(
            "Every process step must be complete before closing the process.",
            exception.Message);
    }

    [Fact]
    public void Close_AfterCompletingAllSteps_ClosesAndAuditsProcess()
    {
        var actorUserId = Guid.NewGuid();
        var process = CreateProcessWithStep();
        var step = process.Steps.Single();

        process.SetStepStatus(
            step.Id,
            ProcessStepStatus.Complete,
            actorUserId,
            "Process User",
            "Verified",
            Now);
        process.Close(actorUserId, "Process User", "Release is ready", Now);

        Assert.Equal(ProcessStatus.Closed, process.Status);
        Assert.Equal(actorUserId, process.ClosedByUserId);
        Assert.Equal("Release is ready", process.FinalNote);
        Assert.Contains(
            process.AuditEvents,
            auditEvent => auditEvent.Action == "Process closed");
    }

    [Fact]
    public void SetStepStatus_WithSequentialSteps_RequiresEarlierCompletion()
    {
        var process = CreateSequentialProcess();
        var steps = process.Steps.OrderBy(step => step.Order).ToArray();

        var blocked = () => process.SetStepStatus(
            steps[1].Id,
            ProcessStepStatus.InProgress,
            Guid.NewGuid(),
            "Process User",
            null,
            Now);

        Assert.Throws<InvalidOperationException>(blocked);

        process.SetStepStatus(
            steps[0].Id,
            ProcessStepStatus.Complete,
            Guid.NewGuid(),
            "Process User",
            "Done",
            Now);
        process.SetStepStatus(
            steps[1].Id,
            ProcessStepStatus.InProgress,
            Guid.NewGuid(),
            "Process User",
            null,
            Now);

        Assert.Equal(ProcessStepStatus.InProgress, steps[1].Status);
    }

    [Fact]
    public void Reopen_WithSequentialSteps_RejectsStartedLaterStep()
    {
        var process = CreateSequentialProcess();
        var steps = process.Steps.OrderBy(step => step.Order).ToArray();
        process.SetStepStatus(
            steps[0].Id,
            ProcessStepStatus.Complete,
            Guid.NewGuid(),
            "Process User",
            "Done",
            Now);
        process.SetStepStatus(
            steps[1].Id,
            ProcessStepStatus.InProgress,
            Guid.NewGuid(),
            "Process User",
            null,
            Now);

        var reopen = () => process.SetStepStatus(
            steps[0].Id,
            ProcessStepStatus.InProgress,
            Guid.NewGuid(),
            "Process User",
            null,
            Now);

        Assert.Throws<InvalidOperationException>(reopen);
    }

    private static ProcessInstance CreateProcessWithStep()
    {
        var process = ProcessInstance.Create(
            Guid.NewGuid(),
            1,
            "Release readiness",
            "Release 1.0",
            Guid.NewGuid(),
            "Process Owner",
            Now);

        process.AddStep(
            Guid.NewGuid(),
            1,
            "Complete QA",
            null,
            "QA",
            "Run the release test suite.",
            null,
            null,
            null,
            dueAtUtc: null,
            noteRequired: true);

        return process;
    }

    private static ProcessInstance CreateSequentialProcess()
    {
        var process = ProcessInstance.Create(
            Guid.NewGuid(),
            1,
            "Sequential process",
            "Release 1.0",
            Guid.NewGuid(),
            "Process Owner",
            Now,
            requireSequentialSteps: true);

        process.AddStep(
            Guid.NewGuid(),
            1,
            "First step",
            null,
            "Any active user",
            "",
            null,
            null,
            null,
            dueAtUtc: null,
            noteRequired: false);
        process.AddStep(
            Guid.NewGuid(),
            2,
            "Second step",
            null,
            "Any active user",
            "",
            null,
            null,
            null,
            dueAtUtc: null,
            noteRequired: false);

        return process;
    }
}
