using System.Text.RegularExpressions;
using ExileCore.PoEMemory.MemoryObjects;

namespace WhatsAHeist;

public enum HeistPhase
{
    Infiltration,
    Escape
}

public enum HeistNodeType
{
    Start,
    Node,
    Endpoint
}

public class HeistPathNodeData
{
    public Entity Entity { get; set; }
    public System.Numerics.Vector2 GridPos { get; set; }
    public HeistNodeType NodeType { get; }
    public bool IsVisited { get; set; }

    public HeistPathNodeData(Entity entity, HeistNodeType nodeType)
    {
        Entity = entity;
        NodeType = nodeType;
        GridPos = entity.GridPosNum;
    }
}

public class HeistDoorData
{
    private static readonly Regex JobRegex = new(@"Door_NPC(\w+?)(?:Slide|Bash|Break|Pick|Open|Force)?$", RegexOptions.Compiled);

    public Entity Entity { get; set; }
    public System.Numerics.Vector2 GridPos { get; set; }
    public bool IsClosed { get; set; }
    public string RequiredJob { get; }

    public HeistDoorData(Entity entity)
    {
        Entity = entity;
        IsClosed = true;
        GridPos = entity.GridPosNum;

        var match = JobRegex.Match(entity.Path);
        RequiredJob = match.Success ? match.Groups[1].Value : "Unknown";
    }
}

public class HeistChestData
{
    private static readonly Regex RewardRegex = new(@"Heist.*?Chest.*?(\w+)$", RegexOptions.Compiled);

    public Entity Entity { get; }
    public bool IsOpened { get; set; }
    public string RewardType { get; }

    public HeistChestData(Entity entity)
    {
        Entity = entity;
        IsOpened = false;

        var match = RewardRegex.Match(entity.Path);
        RewardType = match.Success ? match.Groups[1].Value : "Reward";
    }
}
