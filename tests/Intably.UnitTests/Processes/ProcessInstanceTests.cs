using Intably.Domain.Common;
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
        Assert.False(process.CanUpdateStep(step.Id));
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

    [Fact]
    public void PrerequisiteGroup_UnlocksOnlyAfterEveryStepCompletes()
    {
        var process = CreateProcess();
        var prerequisite = process.AddStepGroup(
            Guid.NewGuid(),
            "Preparation",
            "",
            1,
            StepGroupExecutionMode.Parallel);
        var dependent = process.AddStepGroup(
            Guid.NewGuid(),
            "Execution",
            "",
            2,
            StepGroupExecutionMode.Parallel);
        process.AddStepGroupPrerequisite(dependent.Id, prerequisite.Id);
        var first = AddStep(process, prerequisite.Id, 1, "First");
        var second = AddStep(process, prerequisite.Id, 2, "Second");
        var dependentStep = AddStep(process, dependent.Id, 1, "Dependent");

        Assert.False(process.CanUpdateStep(dependentStep.Id));
        Complete(process, first);
        Assert.False(process.CanUpdateStep(dependentStep.Id));
        Complete(process, second);
        Assert.True(process.CanUpdateStep(dependentStep.Id));
    }

    [Fact]
    public void Reopen_RejectsStartedTransitivelyDependentGroup()
    {
        var process = CreateProcess();
        var firstGroup = process.AddStepGroup(
            Guid.NewGuid(), "First", "", 1, StepGroupExecutionMode.Parallel);
        var secondGroup = process.AddStepGroup(
            Guid.NewGuid(), "Second", "", 2, StepGroupExecutionMode.Parallel);
        var thirdGroup = process.AddStepGroup(
            Guid.NewGuid(), "Third", "", 3, StepGroupExecutionMode.Parallel);
        process.AddStepGroupPrerequisite(secondGroup.Id, firstGroup.Id);
        process.AddStepGroupPrerequisite(thirdGroup.Id, secondGroup.Id);
        var first = AddStep(process, firstGroup.Id, 1, "First");
        var second = AddStep(process, secondGroup.Id, 1, "Second");
        var third = AddStep(process, thirdGroup.Id, 1, "Third");
        Complete(process, first);
        Complete(process, second);
        process.SetStepStatus(
            third.Id,
            ProcessStepStatus.InProgress,
            Guid.NewGuid(),
            "Process User",
            null,
            Now);

        var reopen = () => process.SetStepStatus(
            first.Id,
            ProcessStepStatus.InProgress,
            Guid.NewGuid(),
            "Process User",
            null,
            Now);

        Assert.Throws<InvalidOperationException>(reopen);
    }

    private static ProcessInstance CreateProcessWithStep()
    {
        var process = CreateProcess();
        var group = process.AddStepGroup(
            Guid.NewGuid(),
            "Default",
            "",
            1,
            StepGroupExecutionMode.Parallel);

        process.AddStep(
            Guid.NewGuid(),
            group.Id,
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

    private static ProcessInstance CreateProcess()
    {
        return ProcessInstance.Create(
            Guid.NewGuid(),
            1,
            "Release readiness",
            "Release 1.0",
            Guid.NewGuid(),
            "Process Owner",
            Now);
    }

    private static ProcessStep AddStep(
        ProcessInstance process,
        Guid groupId,
        int order,
        string title)
    {
        return process.AddStep(
            Guid.NewGuid(),
            groupId,
            order,
            title,
            null,
            "Any active user",
            "",
            null,
            null,
            null,
            dueAtUtc: null,
            noteRequired: false);
    }

    private static void Complete(ProcessInstance process, ProcessStep step)
    {
        process.SetStepStatus(
            step.Id,
            ProcessStepStatus.Complete,
            Guid.NewGuid(),
            "Process User",
            null,
            Now);
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
            Now);
        var group = process.AddStepGroup(
            Guid.NewGuid(),
            "Default",
            "",
            1,
            StepGroupExecutionMode.Sequential);

        process.AddStep(
            Guid.NewGuid(),
            group.Id,
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
            group.Id,
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
