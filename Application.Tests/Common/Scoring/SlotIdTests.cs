using Application.Common.Scoring;

namespace Application.Tests.Common.Scoring;


public class SlotIdTests
{
    [Fact]
    public void AllInOrder_ContainsExactly27Slots()
    {


        Assert.Equal(27, SlotId.AllInOrder.Count);
    }

    [Fact]
    public void AllInOrder_AndKnownSet_AreConsistent()
    {
        Assert.Equal(SlotId.AllInOrder.Count, SlotId.KnownSet.Count);
        foreach (var id in SlotId.AllInOrder)
            Assert.Contains(id, SlotId.KnownSet);
    }

    [Fact]
    public void AllInOrder_HasNoDuplicates()
    {
        var distinct = SlotId.AllInOrder.Distinct().ToList();
        Assert.Equal(SlotId.AllInOrder.Count, distinct.Count);
    }

    [Fact]
    public void SlotIds_HaveUniqueIdStrings()
    {
        var ids = SlotId.AllInOrder.Select(s => s.Id).ToList();
        var unique = ids.Distinct().Count();
        Assert.Equal(ids.Count, unique);
    }

    [Fact]
    public void WellKnownSlots_AreInRegistry()
    {

        Assert.Contains(SlotId.Header, SlotId.KnownSet);
        Assert.Contains(SlotId.OutputSpec, SlotId.KnownSet);
        Assert.Contains(SlotId.PreComputedYears, SlotId.KnownSet);
        Assert.Contains(SlotId.HardCapsStep1, SlotId.KnownSet);
        Assert.Contains(SlotId.HardCapsStep2Map, SlotId.KnownSet);
        Assert.Contains(SlotId.EngineeringMgrRule, SlotId.KnownSet);
        Assert.Contains(SlotId.VerdictBands, SlotId.KnownSet);
        Assert.Contains(SlotId.Finale, SlotId.KnownSet);
    }

    [Fact]
    public void UnknownSlotId_NotInRegistry()
    {


        var typo = new SlotId("S007_HARD_CAPS_STEP_4");
        Assert.DoesNotContain(typo, SlotId.KnownSet);
    }
}
