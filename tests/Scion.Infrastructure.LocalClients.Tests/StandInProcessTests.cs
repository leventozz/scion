namespace Scion.Infrastructure.LocalClients.Tests;

/// <summary>
/// The process tests poll the id files the stand-in writes while they are written. A file that is still open in the writer
/// is "no id yet", not an error: the first guard runs that failed (2 of 5 warm runs) failed with exactly that sharing violation,
/// which has nothing to do with the job object.
/// </summary>
[Collection("StandIn")]
public class StandInProcessTests : IDisposable
{
    private readonly StandInProcess _fake = new();

    public void Dispose() => _fake.Dispose();

    [Fact]
    public void A_child_id_file_that_is_still_being_written_is_no_id_yet()
    {
        using var writer = new FileStream(_fake.PidPath + ".child", FileMode.Create, FileAccess.Write, FileShare.None);

        Assert.Null(_fake.ChildId());
    }

    [Fact]
    public void The_child_id_is_there_once_the_writer_has_finished()
    {
        File.WriteAllText(_fake.PidPath + ".child", "4242");

        Assert.Equal(4242, _fake.ChildId());
    }

    [Fact]
    public void An_id_file_that_is_missing_or_holds_no_number_is_no_id()
    {
        Assert.Null(_fake.ChildId());
        Assert.Null(_fake.Id());

        File.WriteAllText(_fake.PidPath, "");

        Assert.Null(_fake.Id());
    }

    [Fact]
    public void The_id_of_the_stand_in_itself_is_read_the_same_way()
    {
        using (new FileStream(_fake.PidPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            Assert.Null(_fake.Id());
        }

        File.WriteAllText(_fake.PidPath, "77");

        Assert.Equal(77, _fake.Id());
    }
}
