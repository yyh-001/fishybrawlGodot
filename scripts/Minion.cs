using System.Collections.Generic;

public class Minion
{
    public string MinionId { get; set; }
    public string Name { get; set; }
    public int Attack { get; set; }
    public int Health { get; set; }
    public int Tier { get; set; }
    public string Tribe { get; set; }
    public List<string> Abilities { get; set; }
    public string Battlecry { get; set; }
    public string Deathrattle { get; set; }
    public string Description { get; set; }
} 