﻿using Newtonsoft.Json;

namespace Chaos
{
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class Attributes
    {
        //baseValues
        [JsonProperty]
        internal byte BaseStr;
        [JsonProperty]
        internal byte BaseInt;
        [JsonProperty]
        internal byte BaseWis;
        [JsonProperty]
        internal byte BaseCon;
        [JsonProperty]
        internal byte BaseDex;
        [JsonProperty]
        internal uint BaseHP;
        [JsonProperty]
        internal uint BaseMP;

        //addedValues
        internal byte AddedStr;
        internal byte AddedInt;
        internal byte AddedWis;
        internal byte AddedCon;
        internal byte AddedDex;
        internal byte AddedHP;
        internal byte AddedMP;

        //Primary
        [JsonProperty]
        internal byte Level;
        [JsonProperty]
        internal byte Ability;

        internal uint MaximumHP => BaseHP + AddedHP;
        internal uint MaximumMP => BaseMP + AddedMP;
        internal byte CurrentStr => (byte)(BaseStr + AddedStr);
        internal byte CurrentInt => (byte)(BaseInt + AddedInt);
        internal byte CurrentWis => (byte)(BaseWis + AddedWis);
        internal byte CurrentCon => (byte)(BaseCon + AddedCon);
        internal byte CurrentDex => (byte)(BaseDex + AddedDex);
        internal bool HasUnspentPoints => UnspentPoints != 0;

        [JsonProperty]
        internal byte UnspentPoints;
        internal short MaximumWeight => (short)(40 + (BaseStr / 2));
        internal short CurrentWeight;

        //Vitality
        [JsonProperty]
        internal uint CurrentHP;
        [JsonProperty]
        internal uint CurrentMP;

        //Experience
        [JsonProperty]
        internal uint Experience;
        [JsonProperty]
        internal uint ToNextLevel;
        [JsonProperty]
        internal uint AbilityExp;
        [JsonProperty]
        internal uint ToNextAbility;
        [JsonProperty]
        internal uint GamePoints;
        [JsonProperty]
        internal uint Gold;

        //Secondary
        internal byte Blind;
        internal MailFlag MailFlags;
        internal Element OffenseElement;
        internal Element DefenseElement;
        internal byte MagicResistance;
        internal sbyte ArmorClass;
        internal byte Dmg;
        internal byte Hit;

        internal Attributes()
        {
            BaseStr = 3;
            BaseInt = 3;
            BaseWis = 3;
            BaseCon = 3;
            BaseDex = 3;
            BaseHP = 100;
            BaseMP = 100;
            Level = 1;
            Ability = 0;
            AddedHP = 0;
            AddedMP = 0;
            AddedStr = 0;
            AddedInt = 0;
            AddedWis = 0;
            AddedCon = 0;
            AddedDex = 0;
            UnspentPoints = 0;
            CurrentWeight = 0;
            CurrentHP = 100;
            CurrentMP = 100;
            Experience = 0;
            ToNextLevel = 150;
            AbilityExp = 0;
            ToNextAbility = 0;
            GamePoints = 0;
            Gold = 0;
            Blind = 0;
            MailFlags = MailFlag.None;
            OffenseElement = Element.None;
            DefenseElement = Element.None;
            MagicResistance = 0;
            ArmorClass = 0;
            Dmg = 0;
            Hit = 0;
        }

        [JsonConstructor]
        internal Attributes(byte baseStr, byte baseInt, byte baseWis, byte baseCon, byte baseDex, uint baseHp, uint baseMp, byte level, byte ability, byte unspentPoints, uint currentHP, uint currentMP, uint experience, uint toNextLevel, uint abilityExp, uint toNextAbility, uint gamePoints, uint gold)
        {
            BaseStr = baseStr;
            BaseInt = baseInt;
            BaseWis = baseWis;
            BaseCon = baseCon;
            BaseDex = baseDex;
            BaseHP = baseHp;
            BaseMP = baseMp;
            Level = level;
            Ability = ability;
            UnspentPoints = unspentPoints;
            CurrentHP = currentHP;
            CurrentMP = currentMP;
            Experience = experience;
            ToNextLevel = toNextLevel;
            AbilityExp = abilityExp;
            ToNextAbility = toNextAbility;
            GamePoints = gamePoints;
            Gold = gold;
        }
    }
}
