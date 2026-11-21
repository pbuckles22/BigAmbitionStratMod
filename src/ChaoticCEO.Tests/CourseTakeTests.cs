using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CourseTakeTests
{
    [Fact]
    public void Unfinished_market_demands_enrolls()
    {
        var lot = new BareLeaseFact(true, BareLease.EmptyType, "ba:street_ninthavenue", 19);
        var place = lot.Street + " " + lot.Number.ToString();
        var needed = FitOut.Decide(
            new[] { StoreHealth.Healthy("ba:street_seventhavenue 15") },
            place,
            FitOutProgress.Bare(),
            "Market Demands");

        var enrolled = CourseTake.Enroll(needed.Gate, 37, lot.Street, lot.Number);

        Assert.Equal("market-demands", needed.Gate);
        Assert.True(enrolled.Enroll);
        Assert.Equal("market-demands", enrolled.Course);
        Assert.Equal("MarketDemands", CourseTake.Diploma(enrolled.Course));
        Assert.Equal("", enrolled.StillRequired);
        Assert.Contains("course.taken version=" + LoadLog.Version, enrolled.Line);
        Assert.Contains("day=37", enrolled.Line);
        Assert.Contains("course=market-demands", enrolled.Line);
        Assert.Contains("street=" + lot.Street, enrolled.Line);
        Assert.Contains("number=19", enrolled.Line);
        Assert.Contains("call=EducationHelper.Command_CompleteCourse", enrolled.Line);
    }

    [Fact]
    public void Expansion_block_does_not_enroll()
    {
        var notReady = new[]
        {
            new StoreHealth(true, true, false, true, true, "ba:street_seventhavenue 15"),
        };
        var blocked = FitOut.Decide(notReady, "ba:street_ninthavenue 19", FitOutProgress.Bare());
        var enrolled = CourseTake.Enroll(blocked.Gate, 37, "ba:street_ninthavenue", 19);

        Assert.Equal(CourseGate.ExpansionSlug, blocked.Gate);
        Assert.False(enrolled.Enroll);
        Assert.Equal("", enrolled.Line);
        Assert.Equal("", CourseTake.Diploma(blocked.Gate));
        Assert.Equal("", CourseTake.Diploma(null));
        Assert.Equal("", CourseTake.Diploma("   "));
    }

    [Fact]
    public void Headquarters_course_enrolls()
    {
        var enrolled = CourseTake.Enroll("headquarters", 4, "ba:street_seventhavenue", 15);

        Assert.True(enrolled.Enroll);
        Assert.Equal("Headquarters", CourseTake.Diploma(enrolled.Course));
        Assert.Equal("", enrolled.StillRequired);
        Assert.Contains("course=headquarters", enrolled.Line);
        Assert.Contains("call=EducationHelper.Command_CompleteCourse", enrolled.Line);
        Assert.Equal("FoodSafetyCourse", CourseTake.Diploma("food-safety-course"));
        Assert.Equal("", CourseTake.Diploma("customer-service"));
    }

    [Fact]
    public void Record_writes_course_taken_once()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-course-taken-" + Guid.NewGuid().ToString("N")));
        try
        {
            var enrolled = CourseTake.Enroll("market-demands", 37, "ba:street_ninthavenue", 19);
            var first = CourseTake.Record(dir.FullName, enrolled);
            var again = CourseTake.Record(dir.FullName, enrolled);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            var hits = 0;
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line == first)
                {
                    hits++;
                }
            }

            Assert.Equal(first, again);
            Assert.Equal(1, hits);
            Assert.Contains("course=market-demands", first);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_course_needed_enroll_taken_retry()
    {
        // Chain: course.needed → enroll → course.taken → retry
        var lot = new BareLeaseFact(true, BareLease.EmptyType, "ba:street_ninthavenue", 19);
        Assert.True(BareLease.IsBare(lot));
        var place = lot.Street + " " + lot.Number.ToString();
        var priors = new[] { StoreHealth.Healthy("ba:street_seventhavenue 15") };
        var needed = FitOut.Decide(priors, place, FitOutProgress.Bare(), "Market Demands");
        var neededLine = CourseGate.Line(needed.Gate, 37, lot.Street, lot.Number);
        var enrolled = CourseTake.Enroll(needed.Gate, 37, lot.Street, lot.Number);
        var retry = FitOut.Decide(priors, place, FitOutProgress.Bare(), enrolled.StillRequired);

        Assert.Contains("course.needed", neededLine);
        Assert.Contains("course=market-demands", neededLine);
        Assert.Contains("course.taken", enrolled.Line);
        Assert.Contains("course=" + needed.Gate, enrolled.Line);
        Assert.Contains("day=37", enrolled.Line);
        Assert.True(retry.Chosen);
        Assert.Equal("layout", retry.Step);
        Assert.Equal("", retry.Gate);
        Assert.Equal(place, retry.Place);
    }

    [Fact]
    public void Chain_taken_course_retries_into_the_expansion_block()
    {
        // Chain: course.needed → enroll → course.taken → retry
        var lot = new BareLeaseFact(true, "", "ba:street_ninthavenue", 19);
        Assert.True(BareLease.IsBare(lot));
        var place = lot.Street + " " + lot.Number.ToString();
        var notReady = new[]
        {
            new StoreHealth(true, true, false, true, true, "ba:street_seventhavenue 15"),
        };
        var needed = FitOut.Decide(notReady, place, FitOutProgress.Bare(), "Market Demands");
        var enrolled = CourseTake.Enroll(needed.Gate, 37, lot.Street, lot.Number);
        var retry = FitOut.Decide(notReady, place, FitOutProgress.Bare(), enrolled.StillRequired);

        Assert.Equal("market-demands", needed.Gate);
        Assert.True(enrolled.Enroll);
        Assert.Contains("course.taken", enrolled.Line);
        Assert.Equal(CourseGate.ExpansionSlug, retry.Gate);
        Assert.False(retry.Chosen);
        Assert.DoesNotContain("course=market-demands", CourseGate.Line(retry.Gate, 37, lot.Street, lot.Number));
    }
}
