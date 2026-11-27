using Orbs.Domain.Common;
using Orbs.Domain.Jobs;

namespace Orbs.Domain.Tests;

public class JobTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_job_is_due_once_its_time_has_come()
    {
        var job = Job.Create("x", null, Now.AddMinutes(5), Now);

        job.Payload.ShouldBe("{}");
        job.IsDue(Now).ShouldBeFalse();
        job.IsDue(Now.AddMinutes(5)).ShouldBeTrue();
    }

    [Fact]
    public void Failures_retry_with_a_growing_delay_then_give_up()
    {
        var job = Job.Create("x", null, Now, Now, maxAttempts: 3);

        job.Start(Now);
        job.Fail("one", Now);
        job.Status.ShouldBe(JobStatus.Pending);
        job.RunAt.ShouldBe(Now.AddSeconds(30));

        job.Start(job.RunAt);
        job.Fail("two", Now);
        job.RunAt.ShouldBe(Now.AddSeconds(60));

        job.Start(job.RunAt);
        job.Fail("three", Now);
        job.Status.ShouldBe(JobStatus.Failed);
        job.LastError.ShouldBe("three");
        job.Attempts.ShouldBe(3);
    }

    [Fact]
    public void Only_pending_jobs_start()
    {
        var job = Job.Create("x", null, Now, Now);
        job.Start(Now);

        Should.Throw<DomainException>(() => job.Start(Now));
    }

    [Fact]
    public void A_job_interrupted_by_a_restart_goes_back_in_the_queue()
    {
        var job = Job.Create("x", null, Now, Now);
        job.Start(Now);

        job.Requeue(Now.AddMinutes(1));

        job.IsDue(Now.AddMinutes(1)).ShouldBeTrue();
    }

    [Fact]
    public void Long_errors_are_clipped()
    {
        var job = Job.Create("x", null, Now, Now, maxAttempts: 1);
        job.Start(Now);

        job.Fail(new string('e', 5_000), Now);

        job.LastError!.Length.ShouldBe(Job.MaxErrorLength);
    }
}
