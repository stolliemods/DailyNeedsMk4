using VRage.ModAPI;
using System.Xml.Serialization;
using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Utils;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Library.Utils;

namespace Stollie.DailyNeeds {
    public class ConfigData
    {

        public ulong steamid;
		public float MAX_VALUE; // = 100f;
		public float MIN_VALUE; // = -100f; // if less than zero, a severely starved character will have to consume more
		public float HUNGRY_WHEN; // = 0.3f; // if need is this much of maxval, consume
		public float THIRSTY_WHEN; // = 0.3f; // if need is this much of maxval, consume
		public float THIRST_PER_DAY; // = 300f; //600f;
		public float HUNGER_PER_DAY; // = 100f; //300f;
		public float DAMAGE_SPEED_HUNGER; // = -0.15f; // 2; // if negative, scale to minvalue for damage. if positive, do this much damage every tick.
		public float DAMAGE_SPEED_THIRST; // = -0.5f; //5; // if negative, scale to use minvalue for damage.  if positive, do this much damage every tick.
		public float DEFAULT_MODIFIER; // = 1f;
		public float FLYING_MODIFIER; // = 1f;
		public float RUNNING_MODIFIER; // = 1.5f;
		public float SPRINTING_MODIFIER; // = 2f;
		public float NO_MODIFIER; // = 1f;
		public float CRAP_AMOUNT; // = 0.90f; // if zero, skip creating waste, otherwise, make GreyWater and Organic right after eating, and don't go into details
		public float CROSS_CRAP_AMOUNT; // = 0.0f; // does eating/drinking generate any amount of the "other" waste? formula is (1-crapamount)*this
		public float DEATH_RECOVERY; // = 1.25f; // if true, "evacuate" before dying, based on current hunger and thirst level. This number is how much is evacuated if player is at 100%
		public bool FATIGUE_ENABLED;
		public float FATIGUE_SITTING;
		public float FATIGUE_CROUCHING;
		public float FATIGUE_STANDING;
		public float FATIGUE_WALKING;
		public float FATIGUE_RUNNING;
		public float FATIGUE_FLYING;
		public float FATIGUE_SPRINTING;
		public float EXTRA_THIRST_FROM_FATIGUE; // = -0.5f; //5; // if negative, scale to use minvalue for damage.  if positive, do this much damage every tick.
		public float FATIGUE_LEVEL_NOHEALING; // at this fraction of MIN_VALUE, prevent autoheal
		public float FATIGUE_LEVEL_FORCEWALK; // at this fraction of MIN_VALUE, try to force walking
		public float FATIGUE_LEVEL_FORCECROUCH; // at this fraction of MIN_VALUE, try to force walking
		public float FATIGUE_LEVEL_HELMET; // at this fraction of MIN_VALUE, toggle helmet
		public float FATIGUE_LEVEL_HEARTATTACK; // at this fraction of MIN_VALUE, heart attack
		public float STARTING_HUNGER;
		public float STARTING_THIRST;
		public float STARTING_FATIGUE;

        //Determines re-spawn values.
        public float RESPAWN_HUNGER = 31f;
        public float RESPAWN_THIRST = 31f;
        public float RESPAWN_FATIGUE = 51f;

        public String STIMULANT_STRING;
        public String CHICKEN_SOUP_STRING;

        //HUD Values
        public float HUNGER_ICON_POSITION_X;
        public float HUNGER_ICON_POSITION_Y;
        public float THIRST_ICON_POSITION_X;
        public float THIRST_ICON_POSITION_Y;
        public float FATIGUE_ICON_POSITION_X;
        public float FATIGUE_ICON_POSITION_Y;

        public bool RECOLOR_BLOCKS;

        public ConfigData() {
		MAX_VALUE = 100f;
		MIN_VALUE = -100f; // if less than zero, a severely starved character will have to consume more
		HUNGRY_WHEN = 0.40f; // if need is this much of maxval, consume
		THIRSTY_WHEN = 0.40f; // if need is this much of maxval, consume
		THIRST_PER_DAY = 300f; //600f;
		HUNGER_PER_DAY = 100f; //300f;
		DAMAGE_SPEED_HUNGER = -0.2f; // 2; // if negative, scale to minvalue for damage. if positive, do this much damage every tick.
		DAMAGE_SPEED_THIRST = -0.3f; //5; // if negative, scale to minvalue for damage.  if positive, do this much damage every tick.
		DEFAULT_MODIFIER = 1f;
		FLYING_MODIFIER = 1f;
		RUNNING_MODIFIER = 0.6f;
		SPRINTING_MODIFIER = 0.9f;
		NO_MODIFIER = 1f;
		CRAP_AMOUNT = 0.90f; // if zero, skip creating waste, otherwise, make GreyWater and Organic right after eating, and don't go into details
		CROSS_CRAP_AMOUNT = 0.0f; // does eating/drinking generate any amount of the "other" waste? formula is (1-crapamount)*this
		DEATH_RECOVERY = 2.25f; // if true, "evacuate" before dying, based on current hunger and thirst level. This number is how much is evacuated if player is at 100%
		FATIGUE_ENABLED = true;
		FATIGUE_SITTING = 0.25f;
		FATIGUE_CROUCHING = 0.15f;
		FATIGUE_STANDING = 0.1f;
		FATIGUE_WALKING = 0f;
		FATIGUE_RUNNING = -0.015f;
		FATIGUE_FLYING  = -0.001f;
		FATIGUE_SPRINTING = -0.03f;
		EXTRA_THIRST_FROM_FATIGUE = -1.5f; // negative: multiply thirst modifier. positive: add to thirst directly.
		FATIGUE_LEVEL_NOHEALING = 0.01f; // at this fraction of MIN_VALUE, prevent autoheal
		FATIGUE_LEVEL_FORCEWALK = 0.2f; // at this fraction of MIN_VALUE, try to force walking
		FATIGUE_LEVEL_FORCECROUCH = 0.5f; // at this fraction of MIN_VALUE, try to force walking
		FATIGUE_LEVEL_HELMET = 0.85f; // at this fraction of MIN_VALUE, toggle helmet
		FATIGUE_LEVEL_HEARTATTACK = 0.999f; // at this fraction of MIN_VALUE, heart attack
		STARTING_HUNGER = 100f;
		STARTING_THIRST = 100f;
		STARTING_FATIGUE = 100f;
        RESPAWN_HUNGER = 31f;
        RESPAWN_THIRST = 31f;
        RESPAWN_FATIGUE = 51f;
        CHICKEN_SOUP_STRING = "ChickenSoupString"; // effectively disabled
		STIMULANT_STRING = "StimulantString"; // effectively disabled

        //HUD Values
        HUNGER_ICON_POSITION_X = -0.941f;
        HUNGER_ICON_POSITION_Y = 0.90f;
        THIRST_ICON_POSITION_X = -0.941f;
        THIRST_ICON_POSITION_Y = 0.85f;
        FATIGUE_ICON_POSITION_X = -0.941f;
        FATIGUE_ICON_POSITION_Y = 0.80f;

        RECOLOR_BLOCKS = true;

        }
    }
}
