// ****************************************************************************
// This file belongs to the Chaos-Server project.
// 
// This project is free and open-source, provided that any alterations or
// modifications to any portions of this project adhere to the
// Affero General Public License (Version 3).
// 
// A copy of the AGPLv3 can be found in the project directory.
// You may also find a copy at <https://www.gnu.org/licenses/agpl-3.0.html>
// ****************************************************************************

using System;

namespace Chaos
{
    #region Server
    internal enum EncryptionType
    {
        None = 0,
        Normal = 1,
        MD5 = 2
    }
    internal enum ServerType : byte
    {
        Lobby = 0,
        Login = 1,
        World = 2
    }
    internal enum WaitEventResult
    {
        Signaled = 0,
        Abandoned = 128,
        Timeout = 258,
    }
    internal enum ClientOpCodes : byte
    {
        RequestConnectionInfo = 0,
        CreateChar1 = 2,
        Login = 3,
        CreateChar2 = 4,
        RequestMapData = 5,
        ClientWalk = 6,
        Pickup = 7,
        Drop = 8,
        ExitClient = 11,
        Ignore = 13,
        PublicChat = 14,
        UseSpell = 15,
        JoinClient = 16,
        Turn = 17,
        SpaceBar = 19,
        RequestWorldList = 24,
        Whisper = 25,
        ToggleUserOption = 27,
        UseItem = 28,
        Emote = 29,
        DropGold = 36,
        ChangePassword = 38,
        DropItemOnCreature = 41,
        DropGoldOnCreature = 42,
        RequestProfile = 45,
        RequestGroup = 46,
        ToggleGroup = 47,
        SwapSlot = 48,
        RequestRefresh = 56,
        RequestPursuit = 57,
        ReplyDialog = 58,
        Board = 59,
        UseSkill = 62,
        ClickWorldMap = 63,
        ClickObject = 67,
        RemoveEquipment = 68,
        KeepAlive = 69,
        ChangeStat = 71,
        Exchange = 74,
        RequestLoginNotification = 75,
        BeginChant = 77,
        DisplayChant = 78,
        Personal = 79,
        RequestServerTable = 87,
        ChangeSequence = 98,
        RequestHomepage = 104,
        SynchronizeTicks = 117,
        ChangeSocialStatus = 121,
        RequestMetaFile = 123
    }
    internal enum ServerOpCodes : byte
    {
        ConnectionInfo = 0,
        LoginMessage = 2,
        Redirect = 3,
        Location = 4,
        UserId = 5,
        DisplayItemMonster = 7,
        Attributes = 8,
        ServerMessage = 10,
        ClientWalk = 11,
        CreatureWalk = 12,
        PublicChat = 13,
        RemoveObject = 14,
        AddItem = 15,
        RemoveItem = 16,
        CreatureTurn = 17,
        HealthBar = 19,
        MapInfo = 21,
        AddSpell = 23,
        RemoveSpell = 24,
        Sound = 25,
        AnimateUser = 26,
        MapChangeComplete = 31,
        LightLevel = 32,
        RefreshResponse = 34,
        Animation = 41,
        AddSkill = 44,
        RemoveSkill = 45,
        WorldMap = 46,
        DisplayMenu = 47,
        DisplayDialog = 48,
        BulletinBoard = 49,
        Door = 50,
        DisplayUser = 51,
        Profile = 52,
        WorldList = 54,
        AddEquipment = 55,
        RemoveEquipment = 56,
        ProfileSelf = 57,
        EffectsBar = 58,
        KeepAlive = 59,
        MapData = 60,
        Cooldown = 63,
        Exchange = 66,
        CancelCasting = 72,
        RequestPersonal = 73,
        ForceClientPacket = 75,
        ConfirmExit = 76,
        ServerTable = 86,
        MapLoadComplete = 88,
        LoginNotification = 96,
        GroupRequest = 99,
        LobbyControls = 102,
        MapChangePending = 103,
        SynchronizeTicks = 104,
        Metafile = 111,
        AcceptConnection = 126
    }
    #endregion

    #region Custom Flags
    [Flags]
    internal enum Quest : ulong
    {
        None = 0,
        MaribelRobes = 1,
        //add more quest flags here, double each time
    }
    [Flags]
    internal enum Status : ulong
    {
        None = 0,
        Dead = 1,
        //add more statuses here, double each time
    }
    [Flags]
    internal enum UserState : ulong
    {
        None = 0,
        IsChanting = 1,
        UsedBlink = 2,
        //add more user states here, double each time
    }
    [Flags]
    public enum MapFlags : ulong
    {
        Hostile = 1,
        NonHostile = 2,
        NoSpells = 4,
        NoSkills = 8,
        NoChat = 16,
        Snowing = 32,
        PvP = 64
        //and whatever else we decide
    }
    internal enum PursuitIds : ushort
    {
        None = 0,
        ReviveSelf = 1,
        ReviveUser = 2,
        Teleport = 3,
        SummonUser = 4,
        SummonAll = 5,
        KillUser = 6,
        LouresCitizenship = 7,
        BecomeWarrior = 8,
        BecomeWizard = 9,
        BecomePriest = 10,
        BecomeMonk = 11,
        BecomeRogue = 12,
        GiveTatteredRobe = 13,
        ForceGive = 14,
        STRtoINT = 15,
    }
    #endregion

    #region Messages
    internal enum ServerMessageType : byte
    {
        Whisper = 0,        //blue text
        OrangeBar1 = 1,     //orange bar only
        OrangeBar2 = 2,
        ActiveMessage = 3,  //orange bar + shiftF
        OrangeBar3 = 4,
        AdminMessage = 5,   //in USDA, admins use this orange-bar channel
        OrangeBar5 = 6,
        UserOptions = 7,    //user options get sent through this byte
        ScrollWindow = 8,   //window with a scroll bar (like sense)
        NonScrollWindow = 9,//window without a scroll bar (identify item)
        WoodenBoard = 10,
        GroupChat = 11,     //puke green text (group)
        GuildChat = 12,     //olive green text
        ClosePopup = 17,    //closes current popup
        TopRight = 18       //white text top right
    }

    internal enum LoginMessageType : byte
    {
        Confirm = 0,
        ClearNameMessage = 3,
        ClearPswdMessage = 5,
        CharacterDoesntExist = 14,
        WrongPassword = 15
    }

    internal enum PublicMessageType : byte
    {
        Normal = 0,
        Shout = 1,
        Chant = 2
    }

    internal enum MessageColor : byte
    {
        Red = 98,
        Yellow = 99,
        DarkGreen = 100,
        Silver = 101,
        DarkBlue = 102,
        White = 103,
        LighterGray = 104,
        LightGray = 105,
        Gray = 106,
        DarkGray = 107,
        DarkerGray = 108,
        Black = 109,
        HotPink = 111,
        DarkPurple = 112,
        NeonGreen = 113,
        Orange = 115,
        Brown = 116,
    }
    #endregion

    #region Legend
    internal enum MarkIcon : byte
    {
        Yay = 0,
        Warrior = 1,
        Rogue = 2,
        Wizard = 3,
        Priest = 4,
        Monk = 5,
        Heart = 6,
        Victory = 7
    }
    [Flags]
    internal enum MarkColor : byte
    {
        White = 32,
        LightOrange = 50,
        LightYellow = 64,
        Yellow = 68,
        LightGreen = 75,
        Blue = 88,
        LightPink = 96,
        DarkPurple = 100,
        Pink = 105,
        Darkgreen = 125,
        Green = 128,
        Orange = 152,
        Brown = 160,
        Red = 248
    }
    #endregion

    #region DisplayData
    internal enum NameTagStyle : byte
    {
        NeutralHover = 0,
        Hostile = 1,
        FriendlyHover = 2,
        Neutral = 3
    }
    internal enum LanternSize : byte
    {
        None = 0,
        Small = 1,
        Large = 2
    }
    internal enum RestPosition : byte
    {
        None = 0,
        Kneel = 1,
        Lay = 2,
        Sprawl = 3
    }
    internal enum BodyColor : byte
    {
        White = 0,
        Pale = 1,
        Brown = 2,
        Green = 3,
        Yellow = 4,
        Tan = 5,
        Grey = 6,
        LightBlue = 7,
        Orange = 8,
        Purple = 9
    }
    internal enum BodySprite : byte
    {
        None = 0,
        Male = 16,
        Female = 32,
        MaleGhost = 48,
        FemaleGhost = 64,
        MaleInvis = 80,
        FemaleInvis = 96,
        MaleJester = 112,
        MaleHead = 128,
        FemaleHead = 144,
        BlankMale = 160,
        BlankFemale = 176
    }

    internal enum Direction : byte
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
        All = 4,
        Invalid = 255,
    }

    [Flags]
    internal enum Gender : byte
    {
        Male = 1,
        Female = 2,
        Unisex = Male | Female
    }
    #endregion

    #region Attributes
    internal enum Stat
    {
        STR = 1,
        DEX = 2,
        INT = 4,
        WIS = 8,
        CON = 16
    }
    internal enum Element : byte
    {
        None = 0,
        Fire = 1,
        Water = 2,
        Wind = 3,
        Earth = 4,
        Holy = 5,
        Darkness = 6,
        Wood = 7,
        Metal = 8,
        Nature = 9
    }

    internal enum MailFlag : byte
    {
        None = 0,
        Parcel = 1,
        Letter = 16
    }

    internal enum Nation : byte
    {
        None = 1,
        Suomi = 2,
        Loures = 3,
        Mileth = 4,
        Tagor = 5,
        Rucesion = 6,
        Noes = 7
    }
    [Flags]
    internal enum StatUpdateType : byte
    {
        None = 0,
        /// <summary>
        /// mail
        /// </summary>
        UnreadMail = 1,
        Unknown = 2,
        /// <summary>
        /// Blind, Mail, Elements, Ressists, AC, DMG, HIT
        /// </summary>
        Secondary = 4,
        /// <summary>
        /// Exp, Gold
        /// </summary>
        ExpGold = 8,
        /// <summary>
        /// Current HP, Current MP
        /// </summary>
        Vitality = 16,
        /// <summary>
        /// Level, Max HP/MP, Current stats, Weight, Unspent
        /// </summary>
        Primary = 32,
        GameMasterA = 64,
        GameMasterB = 128,
        Swimming = GameMasterA | GameMasterB,
        Full = Primary | Vitality | ExpGold | Secondary,
    }
    #endregion

    #region Profile
    internal enum BaseClass : byte
    {
        Peasant = 0,
        Warrior = 1,
        Rogue = 2,
        Wizard = 3,
        Priest = 4,
        Monk = 5,
        Admin = 6
    }
    internal enum AdvClass : byte
    {
        None = 0,
        Gladiator = 1,
        Druid = 2,
        Archer = 3,
        Bard = 4,
        Summoner = 5
    }
    internal enum EquipmentSlot : byte
    {
        None = 0,
        Weapon = 1,
        Armor = 2,
        Shield = 3,
        Helmet = 4,
        Earrings = 5,
        Necklace = 6,
        LeftRing = 7,
        RightRing = 8,
        LeftGaunt = 9,
        RightGaunt = 10,
        Belt = 11,
        Greaves = 12,
        Boots = 13,
        Accessory1 = 14,
        Overcoat = 15,
        OverHelm = 16,
        Accessory2 = 17,
        Accessory3 = 18
    }
    #endregion

    #region Options
    internal enum SocialStatus : byte
    {
        Awake = 0,
        DoNotDisturb = 1,
        DayDreaming = 2,
        NeedGroup = 3,
        Grouped = 4,
        LoneHunter = 5,
        GroupHunting = 6,
        NeedHelp = 7
    }
    internal enum UserOption
    {
        Request = 0,
        Whisper = 1,
        Group = 2,
        Shout = 3,
        Wisdom = 4,
        Magic = 5,
        Exchange = 6,
        FastMove = 7,
        GuildChat = 8
    }
    #endregion

    #region GuI
    internal enum EffectsBarColor : byte
    {
        None = 0,
        Blue = 1,
        Green = 2,
        Yellow = 3,
        Orange = 4,
        Red = 5,
        White = 6
    }
    internal enum PanelType : byte
    {
        Inventory = 0,
        SpellBook = 1,
        SkillBook = 2,
        Equipment = 3
    }

    internal enum MenuType : byte
    {
        Menu = 0,
        MenuWithArgs = 1,
        TextEntry = 2,
        WithdrawlOrBuy = 4,
        DepositOrSell = 5,
        LearnSpell = 6,
        LearnSkills = 7,
        DisplaySpells = 8,
        DisplaySkills = 9,
        Dialog = 255
    }
    internal enum DialogArgsType : byte
    {
        None = 0,
        MenuResponse = 1,
        TextResponse = 2
    }
    internal enum DialogType : byte
    {
        Normal = 0,
        ItemMenu = 2,
        TextEntry = 4,
        Speak = 5,
        CreatureMenu = 6,
        Protected = 9,
        CloseDialog = 10
    }

    internal enum DialogOption : sbyte
    {
        Previous = -1,
        Close = 0,
        Next = 1
    }
    internal enum BoardType : byte
    {
        BoardList = 1,
        PublicBoard = 2,
        PublicPost = 3,
        PrivateBoard = 4,
        PrivatePost = 5,
        Message1 = 6,
        Message2 = 7
    }

    internal enum BoardRequestType : byte
    {
        BoardList = 1,
        ViewBoard = 2,
        ViewPost = 3,
        NewPost = 4,
        Delete = 5,
        SendMail = 6,
        Highlight = 7
    }
    #endregion

    #region Skill/Spell

    internal enum SpellType : byte
    {
        None = 0,
        Prompt = 1,
        Targeted = 2,
        Prompt4Nums = 3,
        Prompt3Nums = 4,
        NoTarget = 5,
        Prompt2Nums = 6,
        Prompt1Num = 7,
    }
    internal enum TargetsType : byte
    {
        //skills(usually)
        None = 0,
        Self = 1,
        Front = 2,
        Surround = 3,
        Cleave = 4,
        StraightProjectile = 5,




        //spells(usually)
        Cluster1 = 252,
        Cluster2 = 253,
        Cluster3 = 254,
        Screen = 255
    }
    internal enum BodyAnimation : byte
    {
        None = 0,
        Assail = 1,
        HandsUp = 6,
        Smile = 9,
        Cry = 10,
        Frown = 11,
        Wink = 12,
        Surprise = 13,
        Tongue = 14,
        Pleasant = 15,
        Snore = 16,
        Mouth = 17,
        BlowKiss = 21,
        Wave = 22,
        RockOn = 23,
        Peace = 24,
        Stop = 25,
        Ouch = 26,
        Impatient = 27,
        Shock = 28,
        Pleasure = 29,
        Love = 30,
        SweatDrop = 31,
        Whistle = 32,
        Irritation = 33,
        Silly = 34,
        Cute = 35,
        Yelling = 36,
        Mischievous = 37,
        Evil = 38,
        Horror = 39,
        PuppyDog = 40,
        StoneFaced = 41,
        Tears = 42,
        FiredUp = 43,
        Confused = 44,
        PriestCast = 128,
        TwoHandAtk = 129,
        Jump = 130,
        Kick = 131,
        Punch = 132,
        RoundHouseKick = 133,
        Stab = 134,
        DoubleStab = 135,
        WizardCast = 136,
        PlayNotes = 137,
        HandsUp2 = 138,
        Swipe = 139,
        HeavySwipe = 140,
        JumpAttack = 141,
        BowShot = 142,
        HeavyBowShot = 143,
        LongBowShot = 144,
        Summon = 145,
    }

    #endregion

    #region Game
    internal enum ExchangeType : byte
    {
        StartExchange = 0,
        RequestAmount = 1,
        AddItem = 2,
        SetGold = 3,
        Cancel = 4,
        Accept = 5
    }

    internal enum GameObjectType : byte
    {
        Merchant = 1,
        Item = 2,
        Misc = 4
    }

    internal enum IgnoreType : byte
    {
        Request = 1,
        AddUser = 2,
        RemoveUser = 3
    }
    internal enum CreatureType : byte
    {
        Normal = 0,
        WalkThrough = 1,
        Merchant = 2,
        WhiteSquare = 3,
        User = 4,
    }
    internal enum GroupRequestType : byte
    {
        Request = 1,
        Invite = 2,
        Join = 3,
        Groupbox = 4,
        RemoveGroupBox = 6
    }
    internal enum LightLevel : byte
    {
        Darkest = 0,
        Darker = 1,
        Dark = 2,
        Light = 3,
        Lighter = 4,
        Lightest = 5
    }
    #endregion
}
