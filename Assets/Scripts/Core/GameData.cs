using UnityEngine;

namespace RtsWarcraft
{
    // -----------------------------------------------------------------------
    //  Static game database.  Everything balance related lives here so that
    //  designers can tune the game without touching logic code.
    // -----------------------------------------------------------------------

    public struct UnitDef
    {
        public UnitId id;
        public string displayName;
        public FactionId faction;
        public UnitClass unitClass;

        public int goldCost;
        public int lumberCost;
        public int supply;          // food consumed
        public float trainTime;     // seconds

        public float maxHp;
        public float hpRegen;
        public float maxMana;
        public float manaRegen;
        public int armor;

        public float attackDamage;
        public float attackRange;       // world units
        public float attackCooldown;    // seconds between attacks
        public AttackDamageType damageType;
        public bool rangedAttack;
        public float projectileSpeed;   // 0 => instant / melee

        public float moveSpeed;
        public float turnRate;          // degrees per second
        public float radius;            // collision / selection radius
        public float sightRange;

        public int carryCapacity;       // workers only
        public float harvestTime;       // workers only
        public BuildingId trainsAt;
        public bool isHero;
        public int heroPrimaryAttribute; // 0 str, 1 agi, 2 int
        public Vector3 visualScale;
    }

    public struct BuildingDef
    {
        public BuildingId id;
        public string displayName;
        public FactionId faction;

        public int goldCost;
        public int lumberCost;
        public int supplyProvided;
        public float buildTime;         // seconds for a worker to construct
        public float maxHp;
        public int armor;

        public int sizeX;               // footprint in tiles
        public int sizeZ;

        public float attackDamage;      // towers only (0 = no attack)
        public float attackRange;
        public float attackCooldown;

        public UnitId[] trains;
        public BuildingId[] enablesBuildings;
        public float sightRange;
        public bool isResourceDropoff;
    }

    public struct AbilityDef
    {
        public AbilityId id;
        public string displayName;
        public string hotkey;
        public int manaCost;
        public float cooldown;
        public float range;
        public float radius;            // AoE radius, 0 for single target
        public float power;             // damage / heal amount
        public string description;
        public Color iconColor;
    }

    public static class GameData
    {
        public const int MapSize = 128;             // tiles (square map)
        public const float TileSize = 1.0f;         // world units per tile

        public const int StartGold = 500;
        public const int StartLumber = 150;
        public const int StartSupplyCap = 10;
        public const int MaxSupply = 90;

        public const int WorkerCarry = 10;

        public static readonly Color AllianceColor = new Color(0.20f, 0.45f, 1.00f);
        public static readonly Color HordeColor = new Color(0.95f, 0.22f, 0.18f);
        public static readonly Color SelectionGreen = new Color(0.30f, 1.00f, 0.30f);
        public static readonly Color PreselectYellow = new Color(1.00f, 0.85f, 0.20f);

        // ===================================================================
        //  Units
        // ===================================================================

        public static UnitDef GetUnit(UnitId id)
        {
            switch (id)
            {
                // ---------------- Alliance ----------------
                case UnitId.Peasant:
                    return new UnitDef
                    {
                        id = UnitId.Peasant, displayName = "Peasant", faction = FactionId.Alliance,
                        unitClass = UnitClass.Worker,
                        goldCost = 60, lumberCost = 0, supply = 1, trainTime = 12f,
                        maxHp = 220f, hpRegen = 0.25f, maxMana = 0f, manaRegen = 0f, armor = 0,
                        attackDamage = 5.5f, attackRange = 1.6f, attackCooldown = 1.5f,
                        damageType = AttackDamageType.Normal, rangedAttack = false, projectileSpeed = 0f,
                        moveSpeed = 3.6f, turnRate = 360f, radius = 0.35f, sightRange = 12f,
                        carryCapacity = WorkerCarry, harvestTime = 2.0f,
                        trainsAt = BuildingId.TownHall, isHero = false,
                        visualScale = new Vector3(0.75f, 1.5f, 0.75f)
                    };
                case UnitId.Footman:
                    return new UnitDef
                    {
                        id = UnitId.Footman, displayName = "Footman", faction = FactionId.Alliance,
                        unitClass = UnitClass.Melee,
                        goldCost = 135, lumberCost = 0, supply = 2, trainTime = 18f,
                        maxHp = 420f, hpRegen = 0.25f, maxMana = 0f, manaRegen = 0f, armor = 2,
                        attackDamage = 12.5f, attackRange = 1.8f, attackCooldown = 1.35f,
                        damageType = AttackDamageType.Normal, rangedAttack = false, projectileSpeed = 0f,
                        moveSpeed = 3.4f, turnRate = 360f, radius = 0.4f, sightRange = 12f,
                        trainsAt = BuildingId.Barracks, isHero = false,
                        visualScale = new Vector3(0.85f, 1.7f, 0.85f)
                    };
                case UnitId.Archer:
                    return new UnitDef
                    {
                        id = UnitId.Archer, displayName = "Archer", faction = FactionId.Alliance,
                        unitClass = UnitClass.Ranged,
                        goldCost = 130, lumberCost = 20, supply = 2, trainTime = 17f,
                        maxHp = 245f, hpRegen = 0.25f, maxMana = 0f, manaRegen = 0f, armor = 0,
                        attackDamage = 17f, attackRange = 6.5f, attackCooldown = 1.4f,
                        damageType = AttackDamageType.Piercing, rangedAttack = true, projectileSpeed = 18f,
                        moveSpeed = 3.4f, turnRate = 360f, radius = 0.35f, sightRange = 14f,
                        trainsAt = BuildingId.Barracks, isHero = false,
                        visualScale = new Vector3(0.75f, 1.6f, 0.75f)
                    };
                case UnitId.Knight:
                    return new UnitDef
                    {
                        id = UnitId.Knight, displayName = "Knight", faction = FactionId.Alliance,
                        unitClass = UnitClass.Melee,
                        goldCost = 245, lumberCost = 60, supply = 4, trainTime = 28f,
                        maxHp = 840f, hpRegen = 0.25f, maxMana = 0f, manaRegen = 0f, armor = 4,
                        attackDamage = 26f, attackRange = 1.9f, attackCooldown = 1.4f,
                        damageType = AttackDamageType.Normal, rangedAttack = false, projectileSpeed = 0f,
                        moveSpeed = 4.4f, turnRate = 400f, radius = 0.55f, sightRange = 14f,
                        trainsAt = BuildingId.Barracks, isHero = false,
                        visualScale = new Vector3(1.0f, 1.9f, 1.0f)
                    };
                case UnitId.Paladin:
                    return new UnitDef
                    {
                        id = UnitId.Paladin, displayName = "Paladin", faction = FactionId.Alliance,
                        unitClass = UnitClass.Caster,
                        goldCost = 340, lumberCost = 0, supply = 5, trainTime = 34f,
                        maxHp = 640f, hpRegen = 0.5f, maxMana = 250f, manaRegen = 1.2f, armor = 3,
                        attackDamage = 22f, attackRange = 1.9f, attackCooldown = 1.35f,
                        damageType = AttackDamageType.Normal, rangedAttack = false, projectileSpeed = 0f,
                        moveSpeed = 3.6f, turnRate = 360f, radius = 0.5f, sightRange = 16f,
                        trainsAt = BuildingId.AltarOfKings, isHero = true, heroPrimaryAttribute = 0,
                        visualScale = new Vector3(0.95f, 1.95f, 0.95f)
                    };
                case UnitId.Archmage:
                    return new UnitDef
                    {
                        id = UnitId.Archmage, displayName = "Archmage", faction = FactionId.Alliance,
                        unitClass = UnitClass.Caster,
                        goldCost = 320, lumberCost = 0, supply = 5, trainTime = 32f,
                        maxHp = 480f, hpRegen = 0.4f, maxMana = 350f, manaRegen = 1.6f, armor = 1,
                        attackDamage = 20f, attackRange = 6.0f, attackCooldown = 1.5f,
                        damageType = AttackDamageType.Magic, rangedAttack = true, projectileSpeed = 20f,
                        moveSpeed = 3.4f, turnRate = 360f, radius = 0.45f, sightRange = 16f,
                        trainsAt = BuildingId.AltarOfKings, isHero = true, heroPrimaryAttribute = 2,
                        visualScale = new Vector3(0.9f, 1.9f, 0.9f)
                    };

                // ---------------- Horde ----------------
                case UnitId.Peon:
                    return new UnitDef
                    {
                        id = UnitId.Peon, displayName = "Peon", faction = FactionId.Horde,
                        unitClass = UnitClass.Worker,
                        goldCost = 50, lumberCost = 0, supply = 1, trainTime = 11f,
                        maxHp = 210f, hpRegen = 0.25f, maxMana = 0f, manaRegen = 0f, armor = 0,
                        attackDamage = 5f, attackRange = 1.6f, attackCooldown = 1.5f,
                        damageType = AttackDamageType.Normal, rangedAttack = false, projectileSpeed = 0f,
                        moveSpeed = 3.6f, turnRate = 360f, radius = 0.35f, sightRange = 12f,
                        carryCapacity = WorkerCarry, harvestTime = 2.0f,
                        trainsAt = BuildingId.GreatHall, isHero = false,
                        visualScale = new Vector3(0.8f, 1.45f, 0.8f)
                    };
                case UnitId.Grunt:
                    return new UnitDef
                    {
                        id = UnitId.Grunt, displayName = "Grunt", faction = FactionId.Horde,
                        unitClass = UnitClass.Melee,
                        goldCost = 200, lumberCost = 0, supply = 3, trainTime = 20f,
                        maxHp = 600f, hpRegen = 0.25f, maxMana = 0f, manaRegen = 0f, armor = 1,
                        attackDamage = 16.5f, attackRange = 1.8f, attackCooldown = 1.4f,
                        damageType = AttackDamageType.Normal, rangedAttack = false, projectileSpeed = 0f,
                        moveSpeed = 3.4f, turnRate = 360f, radius = 0.45f, sightRange = 12f,
                        trainsAt = BuildingId.TrollHut, isHero = false,
                        visualScale = new Vector3(0.95f, 1.75f, 0.95f)
                    };
                case UnitId.TrollAxethrower:
                    return new UnitDef
                    {
                        id = UnitId.TrollAxethrower, displayName = "Troll Axethrower", faction = FactionId.Horde,
                        unitClass = UnitClass.Ranged,
                        goldCost = 145, lumberCost = 20, supply = 2, trainTime = 18f,
                        maxHp = 260f, hpRegen = 0.25f, maxMana = 0f, manaRegen = 0f, armor = 0,
                        attackDamage = 19f, attackRange = 6.0f, attackCooldown = 1.45f,
                        damageType = AttackDamageType.Piercing, rangedAttack = true, projectileSpeed = 16f,
                        moveSpeed = 3.4f, turnRate = 360f, radius = 0.35f, sightRange = 14f,
                        trainsAt = BuildingId.TrollHut, isHero = false,
                        visualScale = new Vector3(0.75f, 1.55f, 0.75f)
                    };
                case UnitId.Ogre:
                    return new UnitDef
                    {
                        id = UnitId.Ogre, displayName = "Ogre", faction = FactionId.Horde,
                        unitClass = UnitClass.Melee,
                        goldCost = 280, lumberCost = 70, supply = 4, trainTime = 30f,
                        maxHp = 900f, hpRegen = 0.25f, maxMana = 0f, manaRegen = 0f, armor = 3,
                        attackDamage = 30f, attackRange = 2.0f, attackCooldown = 1.55f,
                        damageType = AttackDamageType.Normal, rangedAttack = false, projectileSpeed = 0f,
                        moveSpeed = 4.0f, turnRate = 360f, radius = 0.6f, sightRange = 14f,
                        trainsAt = BuildingId.TrollHut, isHero = false,
                        visualScale = new Vector3(1.15f, 2.1f, 1.15f)
                    };
                case UnitId.Blademaster:
                    return new UnitDef
                    {
                        id = UnitId.Blademaster, displayName = "Blademaster", faction = FactionId.Horde,
                        unitClass = UnitClass.Caster,
                        goldCost = 360, lumberCost = 0, supply = 5, trainTime = 34f,
                        maxHp = 560f, hpRegen = 0.5f, maxMana = 200f, manaRegen = 1.1f, armor = 3,
                        attackDamage = 28f, attackRange = 1.9f, attackCooldown = 1.3f,
                        damageType = AttackDamageType.Normal, rangedAttack = false, projectileSpeed = 0f,
                        moveSpeed = 3.9f, turnRate = 420f, radius = 0.5f, sightRange = 16f,
                        trainsAt = BuildingId.AltarOfStorms, isHero = true, heroPrimaryAttribute = 1,
                        visualScale = new Vector3(0.95f, 1.9f, 0.95f)
                    };
                case UnitId.FarSeer:
                    return new UnitDef
                    {
                        id = UnitId.FarSeer, displayName = "Far Seer", faction = FactionId.Horde,
                        unitClass = UnitClass.Caster,
                        goldCost = 330, lumberCost = 0, supply = 5, trainTime = 32f,
                        maxHp = 500f, hpRegen = 0.4f, maxMana = 330f, manaRegen = 1.5f, armor = 1,
                        attackDamage = 21f, attackRange = 6.0f, attackCooldown = 1.5f,
                        damageType = AttackDamageType.Magic, rangedAttack = true, projectileSpeed = 20f,
                        moveSpeed = 3.5f, turnRate = 360f, radius = 0.45f, sightRange = 16f,
                        trainsAt = BuildingId.AltarOfStorms, isHero = true, heroPrimaryAttribute = 2,
                        visualScale = new Vector3(0.95f, 1.85f, 0.95f)
                    };
            }
            return default;
        }

        /// <summary>The worker unit type belonging to a faction.</summary>
        public static UnitId WorkerOf(FactionId f)
        {
            return f == FactionId.Alliance ? UnitId.Peasant : UnitId.Peon;
        }

        /// <summary>The town hall (resource drop-off + worker trainer) of a faction.</summary>
        public static BuildingId TownHallOf(FactionId f)
        {
            return f == FactionId.Alliance ? BuildingId.TownHall : BuildingId.GreatHall;
        }

        // ===================================================================
        //  Buildings
        // ===================================================================

        public static BuildingDef GetBuilding(BuildingId id)
        {
            switch (id)
            {
                // ---------------- Alliance ----------------
                case BuildingId.TownHall:
                    return new BuildingDef
                    {
                        id = BuildingId.TownHall, displayName = "Town Hall", faction = FactionId.Alliance,
                        goldCost = 385, lumberCost = 205, supplyProvided = 0, buildTime = 90f,
                        maxHp = 1800f, armor = 4, sizeX = 4, sizeZ = 4,
                        trains = new[] { UnitId.Peasant }, sightRange = 18f, isResourceDropoff = true
                    };
                case BuildingId.Farm:
                    return new BuildingDef
                    {
                        id = BuildingId.Farm, displayName = "Farm", faction = FactionId.Alliance,
                        goldCost = 80, lumberCost = 20, supplyProvided = 6, buildTime = 35f,
                        maxHp = 500f, armor = 0, sizeX = 2, sizeZ = 2,
                        sightRange = 10f
                    };
                case BuildingId.Barracks:
                    return new BuildingDef
                    {
                        id = BuildingId.Barracks, displayName = "Barracks", faction = FactionId.Alliance,
                        goldCost = 160, lumberCost = 60, supplyProvided = 0, buildTime = 50f,
                        maxHp = 1000f, armor = 2, sizeX = 3, sizeZ = 3,
                        trains = new[] { UnitId.Footman, UnitId.Archer, UnitId.Knight },
                        sightRange = 12f
                    };
                case BuildingId.LumberMill:
                    return new BuildingDef
                    {
                        id = BuildingId.LumberMill, displayName = "Lumber Mill", faction = FactionId.Alliance,
                        goldCost = 120, lumberCost = 0, supplyProvided = 0, buildTime = 45f,
                        maxHp = 750f, armor = 1, sizeX = 3, sizeZ = 2,
                        enablesBuildings = new[] { BuildingId.GuardTower }, sightRange = 12f
                    };
                case BuildingId.Blacksmith:
                    return new BuildingDef
                    {
                        id = BuildingId.Blacksmith, displayName = "Blacksmith", faction = FactionId.Alliance,
                        goldCost = 140, lumberCost = 60, supplyProvided = 0, buildTime = 55f,
                        maxHp = 900f, armor = 2, sizeX = 3, sizeZ = 3, sightRange = 12f
                    };
                case BuildingId.GuardTower:
                    return new BuildingDef
                    {
                        id = BuildingId.GuardTower, displayName = "Guard Tower", faction = FactionId.Alliance,
                        goldCost = 110, lumberCost = 40, supplyProvided = 0, buildTime = 40f,
                        maxHp = 500f, armor = 3, sizeX = 2, sizeZ = 2,
                        attackDamage = 24f, attackRange = 9f, attackCooldown = 1.6f, sightRange = 16f
                    };
                case BuildingId.AltarOfKings:
                    return new BuildingDef
                    {
                        id = BuildingId.AltarOfKings, displayName = "Altar of Kings", faction = FactionId.Alliance,
                        goldCost = 180, lumberCost = 50, supplyProvided = 0, buildTime = 55f,
                        maxHp = 700f, armor = 2, sizeX = 3, sizeZ = 3,
                        trains = new[] { UnitId.Paladin, UnitId.Archmage }, sightRange = 12f
                    };
                case BuildingId.ArcaneSanctum:
                    return new BuildingDef
                    {
                        id = BuildingId.ArcaneSanctum, displayName = "Arcane Sanctum", faction = FactionId.Alliance,
                        goldCost = 170, lumberCost = 70, supplyProvided = 0, buildTime = 60f,
                        maxHp = 800f, armor = 2, sizeX = 3, sizeZ = 3, sightRange = 12f
                    };

                // ---------------- Horde ----------------
                case BuildingId.GreatHall:
                    return new BuildingDef
                    {
                        id = BuildingId.GreatHall, displayName = "Great Hall", faction = FactionId.Horde,
                        goldCost = 380, lumberCost = 185, supplyProvided = 0, buildTime = 85f,
                        maxHp = 1800f, armor = 4, sizeX = 4, sizeZ = 4,
                        trains = new[] { UnitId.Peon }, sightRange = 18f, isResourceDropoff = true
                    };
                case BuildingId.Burrow:
                    return new BuildingDef
                    {
                        id = BuildingId.Burrow, displayName = "Burrow", faction = FactionId.Horde,
                        goldCost = 70, lumberCost = 20, supplyProvided = 6, buildTime = 33f,
                        maxHp = 500f, armor = 0, sizeX = 2, sizeZ = 2, sightRange = 10f
                    };
                case BuildingId.WarMill:
                    return new BuildingDef
                    {
                        id = BuildingId.WarMill, displayName = "War Mill", faction = FactionId.Horde,
                        goldCost = 130, lumberCost = 0, supplyProvided = 0, buildTime = 45f,
                        maxHp = 750f, armor = 1, sizeX = 3, sizeZ = 2,
                        enablesBuildings = new[] { BuildingId.WatchTower }, sightRange = 12f
                    };
                case BuildingId.TrollHut:
                    return new BuildingDef
                    {
                        id = BuildingId.TrollHut, displayName = "Troll Hut", faction = FactionId.Horde,
                        goldCost = 170, lumberCost = 70, supplyProvided = 0, buildTime = 52f,
                        maxHp = 1000f, armor = 2, sizeX = 3, sizeZ = 3,
                        trains = new[] { UnitId.Grunt, UnitId.TrollAxethrower, UnitId.Ogre },
                        sightRange = 12f
                    };
                case BuildingId.Stronghold:
                    return new BuildingDef
                    {
                        id = BuildingId.Stronghold, displayName = "Stronghold", faction = FactionId.Horde,
                        goldCost = 150, lumberCost = 60, supplyProvided = 0, buildTime = 55f,
                        maxHp = 900f, armor = 2, sizeX = 3, sizeZ = 3, sightRange = 12f
                    };
                case BuildingId.WatchTower:
                    return new BuildingDef
                    {
                        id = BuildingId.WatchTower, displayName = "Watch Tower", faction = FactionId.Horde,
                        goldCost = 105, lumberCost = 40, supplyProvided = 0, buildTime = 38f,
                        maxHp = 500f, armor = 3, sizeX = 2, sizeZ = 2,
                        attackDamage = 25f, attackRange = 9f, attackCooldown = 1.6f, sightRange = 16f
                    };
                case BuildingId.AltarOfStorms:
                    return new BuildingDef
                    {
                        id = BuildingId.AltarOfStorms, displayName = "Altar of Storms", faction = FactionId.Horde,
                        goldCost = 180, lumberCost = 50, supplyProvided = 0, buildTime = 55f,
                        maxHp = 700f, armor = 2, sizeX = 3, sizeZ = 3,
                        trains = new[] { UnitId.Blademaster, UnitId.FarSeer }, sightRange = 12f
                    };
                case BuildingId.SpiritLodge:
                    return new BuildingDef
                    {
                        id = BuildingId.SpiritLodge, displayName = "Spirit Lodge", faction = FactionId.Horde,
                        goldCost = 170, lumberCost = 70, supplyProvided = 0, buildTime = 60f,
                        maxHp = 800f, armor = 2, sizeX = 3, sizeZ = 3, sightRange = 12f
                    };
            }
            return default;
        }

        /// <summary>Which buildings a worker of this faction may construct.</summary>
        public static BuildingId[] BuildableFor(FactionId f)
        {
            if (f == FactionId.Alliance)
            {
                return new[]
                {
                    BuildingId.Farm, BuildingId.Barracks, BuildingId.LumberMill,
                    BuildingId.Blacksmith, BuildingId.GuardTower,
                    BuildingId.AltarOfKings, BuildingId.ArcaneSanctum
                };
            }
            return new[]
            {
                BuildingId.Burrow, BuildingId.TrollHut, BuildingId.WarMill,
                BuildingId.Stronghold, BuildingId.WatchTower,
                BuildingId.AltarOfStorms, BuildingId.SpiritLodge
            };
        }

        // ===================================================================
        //  Abilities
        // ===================================================================

        public static AbilityDef GetAbility(AbilityId id)
        {
            switch (id)
            {
                case AbilityId.HolyLight:
                    return new AbilityDef
                    {
                        id = AbilityId.HolyLight, displayName = "Holy Light", hotkey = "Q",
                        manaCost = 65, cooldown = 6f, range = 8f, radius = 0f, power = 200f,
                        description = "Heals a friendly unit, or damages an undead/enemy target.",
                        iconColor = new Color(1.0f, 0.95f, 0.55f)
                    };
                case AbilityId.DivineShield:
                    return new AbilityDef
                    {
                        id = AbilityId.DivineShield, displayName = "Divine Shield", hotkey = "W",
                        manaCost = 75, cooldown = 35f, range = 0f, radius = 0f, power = 12f,
                        description = "The Paladin becomes invulnerable for a short time.",
                        iconColor = new Color(0.75f, 0.9f, 1.0f)
                    };
                case AbilityId.Blizzard:
                    return new AbilityDef
                    {
                        id = AbilityId.Blizzard, displayName = "Blizzard", hotkey = "Q",
                        manaCost = 100, cooldown = 15f, range = 9f, radius = 3.5f, power = 75f,
                        description = "Calls down waves of ice that damage every enemy in the area.",
                        iconColor = new Color(0.7f, 0.9f, 1.0f)
                    };
                case AbilityId.SummonWaterElemental:
                    return new AbilityDef
                    {
                        id = AbilityId.SummonWaterElemental, displayName = "Water Elemental", hotkey = "W",
                        manaCost = 125, cooldown = 30f, range = 4f, radius = 0f, power = 60f,
                        description = "Summons a powerful water elemental that fights for you.",
                        iconColor = new Color(0.35f, 0.75f, 1.0f)
                    };
                case AbilityId.CriticalStrike:
                    return new AbilityDef
                    {
                        id = AbilityId.CriticalStrike, displayName = "Critical Strike", hotkey = "Q",
                        manaCost = 0, cooldown = 0f, range = 0f, radius = 0f, power = 2.5f,
                        description = "Passive. Chance to deal bonus damage on every attack.",
                        iconColor = new Color(1.0f, 0.45f, 0.2f)
                    };
                case AbilityId.WindWalk:
                    return new AbilityDef
                    {
                        id = AbilityId.WindWalk, displayName = "Wind Walk", hotkey = "W",
                        manaCost = 75, cooldown = 20f, range = 0f, radius = 0f, power = 10f,
                        description = "Become invisible and move faster until you attack.",
                        iconColor = new Color(0.8f, 1.0f, 0.85f)
                    };
                case AbilityId.ChainLightning:
                    return new AbilityDef
                    {
                        id = AbilityId.ChainLightning, displayName = "Chain Lightning", hotkey = "Q",
                        manaCost = 90, cooldown = 9f, range = 8f, radius = 0f, power = 160f,
                        description = "A bolt of lightning jumps between nearby enemies.",
                        iconColor = new Color(0.6f, 0.8f, 1.0f)
                    };
                case AbilityId.FeralSpirit:
                    return new AbilityDef
                    {
                        id = AbilityId.FeralSpirit, displayName = "Feral Spirit", hotkey = "W",
                        manaCost = 75, cooldown = 25f, range = 4f, radius = 0f, power = 45f,
                        description = "Summons spirit wolves to attack your enemies.",
                        iconColor = new Color(0.55f, 1.0f, 0.6f)
                    };
            }
            return default;
        }

        /// <summary>Active (castable) abilities of a hero.</summary>
        public static AbilityId[] AbilitiesOf(UnitId id)
        {
            switch (id)
            {
                case UnitId.Paladin: return new[] { AbilityId.HolyLight, AbilityId.DivineShield };
                case UnitId.Archmage: return new[] { AbilityId.Blizzard, AbilityId.SummonWaterElemental };
                case UnitId.Blademaster: return new[] { AbilityId.CriticalStrike, AbilityId.WindWalk };
                case UnitId.FarSeer: return new[] { AbilityId.ChainLightning, AbilityId.FeralSpirit };
            }
            return new AbilityId[0];
        }

        /// <summary>Experience required to go from level n to level n+1.</summary>
        public static int XpForLevel(int level)
        {
            return 200 + (level - 1) * 200;
        }

        public const int MaxHeroLevel = 10;
    }
}
