// ---------------------------------------------------------------------------
//  RTS-Warcraft  —  Core enumerations
//  A Warcraft III style RTS built for Unity 2022.3 LTS (built-in RP, uGUI,
//  legacy Input Manager).  No Asset Store / package dependencies required.
// ---------------------------------------------------------------------------

namespace RtsWarcraft
{
    /// <summary>Playable / AI sides.</summary>
    public enum FactionId
    {
        None = 0,
        Alliance = 1,   // Human kingdom  (blue)
        Horde = 2       // Orcish horde   (red)
    }

    public enum ResourceKind
    {
        None = 0,
        Gold = 1,
        Lumber = 2
    }

    /// <summary>How a unit is categorised by the command / AI systems.</summary>
    public enum UnitClass
    {
        Worker = 0,     // harvests, repairs, builds
        Melee = 1,
        Ranged = 2,
        Caster = 3,     // hero or spell caster
        Siege = 4
    }

    public enum UnitId
    {
        None = 0,

        // ---- Alliance ----
        Peasant = 1,
        Footman = 2,
        Archer = 3,
        Knight = 4,
        Paladin = 5,        // hero
        Archmage = 6,       // hero

        // ---- Horde ----
        Peon = 10,
        Grunt = 11,
        TrollAxethrower = 12,
        Ogre = 13,
        Blademaster = 14,   // hero
        FarSeer = 15        // hero
    }

    public enum BuildingId
    {
        None = 0,

        // ---- Alliance ----
        TownHall = 1,
        Farm = 2,
        Barracks = 3,
        LumberMill = 4,
        Blacksmith = 5,
        GuardTower = 6,
        AltarOfKings = 7,
        ArcaneSanctum = 8,

        // ---- Horde ----
        GreatHall = 20,
        Burrow = 21,
        WarMill = 22,
        TrollHut = 23,
        Stronghold = 24,
        WatchTower = 25,
        AltarOfStorms = 26,
        SpiritLodge = 27
    }

    public enum AbilityId
    {
        None = 0,

        // Paladin
        HolyLight = 1,
        DivineShield = 2,
        // Archmage
        Blizzard = 3,
        SummonWaterElemental = 4,
        // Blademaster
        CriticalStrike = 5,
        WindWalk = 6,
        // Far Seer
        ChainLightning = 7,
        FeralSpirit = 8,

        // Generic worker / production buttons (live in the same command card)
        BuildMenu = 50,
        BuildFarm = 51,
        BuildBarracks = 52,
        BuildLumberMill = 53,
        BuildTower = 54,
        BuildAltar = 55,
        Cancel = 60,
        Stop = 61,
        Repair = 62,
        SelectAllWorkers = 63
    }

    public enum OrderType
    {
        None = 0,
        Idle,
        Move,
        AttackMove,
        AttackTarget,
        Harvest,
        ReturnResource,
        Build,
        Repair,
        CastAbility
    }

    public enum AttackDamageType
    {
        Normal = 0,
        Piercing = 1,
        Siege = 2,
        Magic = 3
    }

    /// <summary>High level AI personality states for the enemy player.</summary>
    public enum AiState
    {
        Econ = 0,
        BuildArmy = 1,
        Attack = 2
    }
}
