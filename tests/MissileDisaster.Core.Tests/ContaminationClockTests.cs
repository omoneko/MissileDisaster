using System;
using MissileDisaster.Core;
using Xunit;

public class ContaminationClockTests
{
    [Fact]
    public void Not_expired_before_the_year_span()
    {
        long start = new DateTime(2000, 1, 1).Ticks;
        long now = new DateTime(2040, 1, 1).Ticks; // 40年後 < 50年
        Assert.False(ContaminationClock.HasExpired(start, now, 50));
    }

    [Fact]
    public void Expired_at_or_after_the_year_span()
    {
        long start = new DateTime(2000, 1, 1).Ticks;
        long atExpiry = new DateTime(2050, 1, 1).Ticks; // ちょうど50年
        long past = new DateTime(2051, 1, 1).Ticks;
        Assert.True(ContaminationClock.HasExpired(start, atExpiry, 50));
        Assert.True(ContaminationClock.HasExpired(start, past, 50));
    }
}
