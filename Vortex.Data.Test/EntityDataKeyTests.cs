namespace Vortex.Data.Test;

public class EntityDataKeyTests
{
    [Fact]
    public void NamesTheIndicesOfEachEntityType()
    {
        Assert.Equal(8, EntityDataKeys.IndexOf(EntityType.Item, EntityDataKey.Item));
        Assert.Equal(9, EntityDataKeys.IndexOf(EntityType.Zombie, EntityDataKey.Health));
        Assert.Equal(16, EntityDataKeys.IndexOf(EntityType.Zombie, EntityDataKey.Baby));
        Assert.Equal(EntityDataKey.SharedFlags, EntityDataKeys.Of(EntityType.Player)[0]);
    }

    [Fact]
    public void KnowsWhichTypesLackAField()
        => Assert.Equal(-1, EntityDataKeys.IndexOf(EntityType.Item, EntityDataKey.Health));

    [Fact]
    public void CoversEveryEntityType()
        => Assert.All(Enum.GetValues<EntityType>(), type => Assert.NotEmpty(EntityDataKeys.Of(type)));
}
