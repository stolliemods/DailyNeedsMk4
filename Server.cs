using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Sandbox.Game;
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
using VRage.ModAPI;

namespace Stollie.DailyNeeds
{
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class Server : MySessionComponentBase
	{

        private bool dead = false;

        private int food_logic_skip = 0;
		// internal counter, init at 0
		private const int FOOD_LOGIC_SKIP_TICKS = 60 * 5;
		// Updating every 5s
		private static float MAX_VALUE = 100f;
		private static float MIN_VALUE = -100f;
		// if less than zero, a severely starved character will have to consume more
		private static float HUNGRY_WHEN = 0.3f;
		// if need is this much of maxval, consume
		private static float THIRSTY_WHEN = 0.3f;
		// if need is this much of maxval, consume
		private static float THIRST_PER_DAY = 300f;
		//600f;
		private static float HUNGER_PER_DAY = 100f;
		//300f;
		private static float DAMAGE_SPEED_HUNGER = -0.2f;
		// 2; // if negative, scale to minvalue for damage. if positive, do this much damage every tick.
		private static float DAMAGE_SPEED_THIRST = -0.6f;
		//5; // if negative, scale to use minvalue for damage.  if positive, do this much damage every tick.
		private static float DEFAULT_MODIFIER = 1f;
		private static float FLYING_MODIFIER = 1f;
		private static float RUNNING_MODIFIER = 1.5f;
		private static float SPRINTING_MODIFIER = 2f;
		private static float NO_MODIFIER = 1f;

        // if zero, skip creating waste, otherwise, make GreyWater and Organic right after eating, and don't go into details
        private static float CRAP_AMOUNT = 0.90f;

        // does eating/drinking generate any amount of the "other" waste? formula is (1-crapamount)*this
        private static float CROSS_CRAP_AMOUNT = 0.0f;

        // if true, "evacuate" before dying, based on current hunger and thirst level. This number is how much is evacuated if player is at 100%
        private static float DEATH_RECOVERY = 2.00f;

		private static bool FATIGUE_ENABLED = true;
		private static float FATIGUE_SITTING = 0.1f;
		private static float FATIGUE_CROUCHING = 0.075f;
		private static float FATIGUE_STANDING = 0.05f;
		private static float FATIGUE_FLYING = -0.05f;
		private static float FATIGUE_WALKING = -0.1f;
		private static float FATIGUE_RUNNING = -0.1f;
		private static float FATIGUE_SPRINTING = -0.1f;
		private static float EXTRA_THIRST_FROM_FATIGUE = -0.1f;
		
		private static String CHICKEN_SOUP_STRING = "ChickenSoupString"; // identifies a custom food as a soup (refills both hunger and thirst)
		private static String STIMULANT_STRING = "StimulantString"; // identifies a custom beverage as a stimulant (refills fatigue)

        private static float HUNGER_ICON_POSITION_X = -0.941f;
        private static float HUNGER_ICON_POSITION_Y = 0.90f;
        private static float THIRST_ICON_POSITION_X = -0.941f;
        private static float THIRST_ICON_POSITION_Y = 0.85f;
        private static float FATIGUE_ICON_POSITION_X = -0.941f;
        private static float FATIGUE_ICON_POSITION_Y = 0.80f;

        private static float FATIGUE_LEVEL_NOHEALING = 0.01f; // at this fraction of MIN_VALUE, prevent autoheal
		private static float FATIGUE_LEVEL_FORCEWALK = 0.25f; // at this fraction of MIN_VALUE, try to force walking
		private static float FATIGUE_LEVEL_FORCECROUCH = 0.5f; // at this fraction of MIN_VALUE, try to force walking
		private static float FATIGUE_LEVEL_HELMET = 0.75f; // at this fraction of MIN_VALUE, toggle helmet
		private static float FATIGUE_LEVEL_HEARTATTACK = 0.999f; // at this fraction of MIN_VALUE, heart attack
		
		
		// = -0.6f; //5; // if negative, scale to use minvalue for damage.  if positive, do this much damage every tick.
		private static float FOOD_BONUS = 1.25f; // multipliers
		private static float DRINK_BONUS = 1.25f;
		private static float REST_BONUS = 1.25f;
		
        //Determines starting values for new game/charecter.
		private static float STARTING_HUNGER = 125f;
		private static float STARTING_THIRST = 125f;
		private static float STARTING_FATIGUE = 125f;

        //Determines re-spawn values.
        private static float RESPAWN_HUNGER = 31f;
        private static float RESPAWN_THIRST = 31f;
        private static float RESPAWN_FATIGUE = 51f;

        private float mHungerPerMinute;
		private float mThirstPerMinute;
		private bool IsAutohealingOn = false;
		private float dayLen = 120f;
		private bool config_get = false;

		//private static Config mConfig = Config.Load("hatm.cfg");
		private static PlayerDataStore mPlayerDataStore = new PlayerDataStore();
		private static ConfigDataStore mConfigDataStore = new ConfigDataStore();
		private static List<IMyPlayer> mPlayers = new List<IMyPlayer>();
		private static Dictionary<string, float> mFoodTypes = new Dictionary<string, float>();
		private static Dictionary<string, float> mBeverageTypes = new Dictionary<string, float>();
		private const string OBJECT_BUILDER_PREFIX = "ObjectBuilder_";
		private static bool mStarted = false;

		/*public static void RegisterFood(string szItemName, float hungerValue)
        {
            mFoodTypes.Add(szItemName, hungerValue);
        }

        public static void RegisterBeverage(string szItemName, float thirstValue)
        {
            mBeverageTypes.Add(szItemName, thirstValue);
        }
        
		 */

		private static bool playerEatSomething(IMyEntity entity, PlayerData playerData, float maxval_cap, float crapbonus)
		{
			MyInventoryBase inventory = ((MyEntity)entity).GetInventoryBase();
			var items = inventory.GetItems();

			foreach (IMyInventoryItem item in items) {
				float result;

				// Getting the item type

				string szItemContent = item.Content.ToString();
				string szTypeName = szItemContent.Substring(szItemContent.IndexOf(OBJECT_BUILDER_PREFIX) + OBJECT_BUILDER_PREFIX.Length);

				// Type verification

				if (!szTypeName.Equals("Ingot"))
					continue;
				
				if (mFoodTypes.TryGetValue(item.Content.SubtypeName, out result)) {
					float canConsumeNum = 0f;
					
					// if a food is registered as negative, reduce the maximum value. Useful for low nutrition meals.
					if (result < 0)
					{
						result = Math.Abs(result);
						canConsumeNum = Math.Min((((maxval_cap/2f) - playerData.hunger) / result), (float)item.Amount);
					}
					else
						canConsumeNum = Math.Min(((maxval_cap - playerData.hunger) / result), (float)item.Amount);

					//MyAPIGateway.Utilities.ShowMessage("DEBUG", "canEat: " + canConsumeNum);

					if (canConsumeNum > 0) {
						inventory.Remove(item, (MyFixedPoint)canConsumeNum);
						playerData.hunger += result * (float)canConsumeNum;
						if (item.Content.SubtypeName.Contains("ouillon")) // TODO parametrize this
							playerData.thirst += Math.Max(0f,Math.Min(result * (float)canConsumeNum, maxval_cap-playerData.thirst)); // TODO parametrize this
						else if (item.Content.SubtypeName.Contains(CHICKEN_SOUP_STRING)) // TODO parametrize this
							playerData.thirst += Math.Max(0f,Math.Min(result * (float)canConsumeNum, maxval_cap-playerData.thirst)); // TODO parametrize this
						// waste management line
						if (CRAP_AMOUNT > 0.0) {
							inventory.AddItems((MyFixedPoint)(canConsumeNum * CRAP_AMOUNT * crapbonus), new MyObjectBuilder_Ore() { SubtypeName = "Organic" });
							if (CROSS_CRAP_AMOUNT > 0.0)
								inventory.AddItems((MyFixedPoint)(canConsumeNum * (1 - CRAP_AMOUNT) * CROSS_CRAP_AMOUNT), new MyObjectBuilder_Ingot() { SubtypeName = "GreyWater" });
						}
						return true;
					}
				}
			}

			return false;
		}

		private static bool playerDrinkSomething(IMyEntity entity, PlayerData playerData, float maxval_cap, float crapbonus)
		{
			MyInventoryBase inventory = ((MyEntity)entity).GetInventoryBase();
			var items = inventory.GetItems();

			foreach (IMyInventoryItem item in items) {
				float result;

				// Getting the item type

				string szItemContent = item.Content.ToString();

				//MyAPIGateway.Utilities.ShowMessage("DEBUG", "szItemContent: " + item.Content.SubtypeName);

				string szTypeName = szItemContent.Substring(szItemContent.IndexOf(OBJECT_BUILDER_PREFIX) + OBJECT_BUILDER_PREFIX.Length);

				// Type verification

				if (!szTypeName.Equals("Ingot"))
					continue;
				
				if (mBeverageTypes.TryGetValue(item.Content.SubtypeName, out result)) {
					float canConsumeNum = Math.Min(((maxval_cap - playerData.thirst) / result), (float)item.Amount);

					//MyAPIGateway.Utilities.ShowMessage("DEBUG", "canDrink: " + canConsumeNum);

					if (canConsumeNum > 0) {
						inventory.Remove(item, (MyFixedPoint)canConsumeNum);
						playerData.thirst += result * (float)canConsumeNum;
						if (item.Content.SubtypeName.Contains("offee")) // TODO parametrize this
							playerData.fatigue = MAX_VALUE; // TODO parametrize this
						else if (item.Content.SubtypeName.Contains(STIMULANT_STRING)) // TODO parametrize this
							playerData.fatigue = MAX_VALUE; // TODO parametrize this
						if (item.Content.SubtypeName.Contains("ouillon")) // TODO parametrize this
							playerData.hunger += Math.Max(0f,Math.Min(result * (float)canConsumeNum, maxval_cap-playerData.hunger)); // TODO parametrize this
						else if (item.Content.SubtypeName.Contains(CHICKEN_SOUP_STRING)) // TODO parametrize this
							playerData.hunger += Math.Max(0f,Math.Min(result * (float)canConsumeNum, maxval_cap-playerData.hunger)); // TODO parametrize this
						
						// waste management line
						if (CRAP_AMOUNT > 0.0) {
							inventory.AddItems((MyFixedPoint)(canConsumeNum * CRAP_AMOUNT * crapbonus), new MyObjectBuilder_Ingot() { SubtypeName = "GreyWater" });
							if (CROSS_CRAP_AMOUNT > 0.0)
								inventory.AddItems((MyFixedPoint)(canConsumeNum * (1 - CRAP_AMOUNT) * CROSS_CRAP_AMOUNT), new MyObjectBuilder_Ore() { SubtypeName = "Organic" });
						}
						return true;
					}
				}
			}

			return false;
		}

		private void init()
		{
			mPlayerDataStore.Load();
			mConfigDataStore.Load();
			
			MAX_VALUE = mConfigDataStore.get_MAX_VALUE();
			MIN_VALUE = mConfigDataStore.get_MIN_VALUE();
			HUNGRY_WHEN = mConfigDataStore.get_HUNGRY_WHEN();
			THIRSTY_WHEN = mConfigDataStore.get_THIRSTY_WHEN();
			THIRST_PER_DAY = mConfigDataStore.get_THIRST_PER_DAY();
			HUNGER_PER_DAY = mConfigDataStore.get_HUNGER_PER_DAY();
			DAMAGE_SPEED_HUNGER = mConfigDataStore.get_DAMAGE_SPEED_HUNGER();
			DAMAGE_SPEED_THIRST = mConfigDataStore.get_DAMAGE_SPEED_THIRST();
			DEFAULT_MODIFIER = mConfigDataStore.get_DEFAULT_MODIFIER();
			FLYING_MODIFIER = mConfigDataStore.get_FLYING_MODIFIER();
			RUNNING_MODIFIER = mConfigDataStore.get_RUNNING_MODIFIER();
			SPRINTING_MODIFIER = mConfigDataStore.get_SPRINTING_MODIFIER();
			NO_MODIFIER = mConfigDataStore.get_NO_MODIFIER();
			CRAP_AMOUNT = mConfigDataStore.get_CRAP_AMOUNT();
			CROSS_CRAP_AMOUNT = mConfigDataStore.get_CROSS_CRAP_AMOUNT();
			DEATH_RECOVERY = mConfigDataStore.get_DEATH_RECOVERY();
			
			FATIGUE_ENABLED = mConfigDataStore.get_FATIGUE_ENABLED();
			FATIGUE_SITTING = mConfigDataStore.get_FATIGUE_SITTING();
			FATIGUE_CROUCHING = mConfigDataStore.get_FATIGUE_CROUCHING();
			FATIGUE_STANDING = mConfigDataStore.get_FATIGUE_STANDING();
			FATIGUE_RUNNING = mConfigDataStore.get_FATIGUE_RUNNING();
			FATIGUE_WALKING = mConfigDataStore.get_FATIGUE_WALKING();
			FATIGUE_FLYING = mConfigDataStore.get_FATIGUE_FLYING();
			FATIGUE_SPRINTING = mConfigDataStore.get_FATIGUE_SPRINTING();
			EXTRA_THIRST_FROM_FATIGUE = mConfigDataStore.get_EXTRA_THIRST_FROM_FATIGUE();

			FATIGUE_LEVEL_NOHEALING = mConfigDataStore.get_FATIGUE_LEVEL_NOHEALING();
			FATIGUE_LEVEL_FORCEWALK = mConfigDataStore.get_FATIGUE_LEVEL_FORCEWALK();
			FATIGUE_LEVEL_FORCECROUCH = mConfigDataStore.get_FATIGUE_LEVEL_FORCECROUCH();
			FATIGUE_LEVEL_HELMET = mConfigDataStore.get_FATIGUE_LEVEL_HELMET();
			FATIGUE_LEVEL_HEARTATTACK = mConfigDataStore.get_FATIGUE_LEVEL_HEARTATTACK();			

			STARTING_HUNGER = mConfigDataStore.get_STARTING_HUNGER();
			STARTING_THIRST = mConfigDataStore.get_STARTING_THIRST();
			STARTING_FATIGUE = mConfigDataStore.get_STARTING_FATIGUE();
			
			STIMULANT_STRING = mConfigDataStore.get_STIMULANT_STRING();
			CHICKEN_SOUP_STRING = mConfigDataStore.get_CHICKEN_SOUP_STRING();

            HUNGER_ICON_POSITION_X = mConfigDataStore.get_HUNGER_ICON_POSITION_X();
            HUNGER_ICON_POSITION_Y = mConfigDataStore.get_HUNGER_ICON_POSITION_Y();

            dayLen = Math.Max(MyAPIGateway.Session.SessionSettings.SunRotationIntervalMinutes, 120f);
			mHungerPerMinute = HUNGER_PER_DAY / dayLen;
			mThirstPerMinute = THIRST_PER_DAY / dayLen;
			mConfigDataStore.Save();

			if (MyAPIGateway.Utilities.GamePaths.ModScopeName.Contains(Encoding.UTF8.GetString(Convert.FromBase64String("LnNibQ=="))) == true &&
				(MyAPIGateway.Utilities.GamePaths.ModScopeName.Contains(Encoding.UTF8.GetString(Convert.FromBase64String("MTYwODg0MTY2Nw=="))) == false &&
                (MyAPIGateway.Utilities.GamePaths.ModScopeName.Contains(Encoding.UTF8.GetString(Convert.FromBase64String("MTYyNjE1NzQzNw=="))) == false)))
            {
                return;
            }

			if (Utils.isDev())
				MyAPIGateway.Utilities.ShowMessage("SERVER", "INIT");

            MyAPIGateway.Multiplayer.RegisterMessageHandler(1338, AdminCommandHandler);
			MyAPIGateway.Utilities.RegisterMessageHandler(1339, NeedsApiHandler);

			// Minimum of 2h, because it's unplayable under....

			dayLen = Math.Max(MyAPIGateway.Session.SessionSettings.SunRotationIntervalMinutes, 120f);
			IsAutohealingOn = MyAPIGateway.Session.SessionSettings.AutoHealing;

			mHungerPerMinute = HUNGER_PER_DAY / dayLen;
			mThirstPerMinute = THIRST_PER_DAY / dayLen;

			NeedsApi api = new NeedsApi();
			
			// TODO un-hardcode these

			// Registering drinks

			api.RegisterDrinkableItem("WaterFood", 60f);

			if (FATIGUE_ENABLED)
				api.RegisterDrinkableItem("CoffeeFood", 100f); // gives bonus to fatigue to compensate for less thirst sat
			else
				api.RegisterDrinkableItem("CoffeeFood", 100f); // just better than water. engineers do run on coffee after all

			api.RegisterDrinkableItem("SabiroidBouillon", 35f); // special: counts as both food and drink
			api.RegisterDrinkableItem("WolfBouillon", 35f); // special: counts as both food and drink

			
			// Registering foods TODO move this to a xml file maybe?
			// negative means that they can only refill you to a maximum of MAX_VALUE/2

			api.RegisterEdibleItem("LuxuryMeal", 100f);
			api.RegisterEdibleItem("SabiroidSteak", 50f);
			api.RegisterEdibleItem("WolfSteak", 50f);
			api.RegisterEdibleItem("SabiroidOmelette", 45f);
			api.RegisterEdibleItem("VeganFood", 35f);
			api.RegisterEdibleItem("SubFresh", 25f); // possible to eat raw veggies directly, but less efficient than prepared veggies
			api.RegisterEdibleItem("ArtificialFood", -15f); // works, but very meh. good way to go to the restroom in a hurry though

            // EMERGENCY RATIONS
            api.RegisterEdibleItem("EmergencyFood", -10f); // emergency ration, takes ages to make and only available in emergency rations block
            api.RegisterDrinkableItem("EmergencyWater", -50f); // emergency ration, takes ages to make and only available in emergency rations block

            // ADDED NEW FOODS by SKALLABJORN MANUAL LISTINGS START

            api.RegisterEdibleItem("NotBeefBurger", 75f);
			api.RegisterEdibleItem("ToFurkey", 80f);
			api.RegisterEdibleItem("SpaceMealBar", 60f);
			api.RegisterEdibleItem("SpacersBreakfast", 75f);
			
			
			api.RegisterEdibleItem("HotChocolate", 15f); // food and drink and stim
			
			if (FATIGUE_ENABLED)
				api.RegisterDrinkableItem("HotChocolate", 30f); // gives bonus to fatigue to compensate for less thirst sat
			else
				api.RegisterDrinkableItem("HotChocolate", 45f); // food and drink and stim


			api.RegisterEdibleItem("ProteinShake", 35f); // food and drink and stim
			if (FATIGUE_ENABLED)
				api.RegisterDrinkableItem("ProteinShake", 35f); // gives bonus to fatigue to compensate for less thirst sat
			else
				api.RegisterDrinkableItem("ProteinShake", 50f); // food and drink and stim
			
			
			
			// ADDED NEW FOODS by SKALLABJORN MANUAL LISTINGS END
			
			
			api.RegisterEdibleItem("SabiroidBouillon", 35f); // special: counts as both food and drink
			api.RegisterEdibleItem("WolfBouillon", 35f); // special: counts as both food and drink
				
			// compatibility with the complex mod
			api.RegisterEdibleItem("MossMeal", -10f);
			api.RegisterEdibleItem("WolfBurger", 70f);
			api.RegisterEdibleItem("MartianSpecial", 35f);

			api.RegisterEdibleItem("Tomato", 15f);
			api.RegisterEdibleItem("Carrot", 15f);
			api.RegisterEdibleItem("Cucumber", 15f);
			api.RegisterEdibleItem("Potato", 15f);
			
			// in case of misspellings
			api.RegisterEdibleItem("Tomatos", 15f);
			api.RegisterEdibleItem("Tomatoes", 15f);
			api.RegisterEdibleItem("Carrots", 15f);
			api.RegisterEdibleItem("Cucumbers", 15f);
			api.RegisterEdibleItem("Potatos", 15f);
			api.RegisterEdibleItem("Potatoes", 15f);
			
			
			// this lets other people do stuff with this, just add the appropriate tag to the food name. Use whole numbers. For example, SpacePasta!fih40 or SpaceCola!di60 or SpaceTofu!fil20
			
			for (int i=1;i<=100;i++)
			{
				api.RegisterEdibleItem("!fil"+(i),(float)(-i));
				api.RegisterEdibleItem("!fih"+(i),(float)(i));
				api.RegisterDrinkableItem("!di"+(i),(float)(i));
			}
		}

		// Update the player list

		private void updatePlayerList()
		{
			mPlayers.Clear();
			MyAPIGateway.Players.GetPlayers(mPlayers);
		}

		private IMyEntity GetCharacterEntity(IMyEntity entity)
		{
			if (entity is MyCockpit)
				return (entity as MyCockpit).Pilot as IMyEntity;

			if (entity is MyRemoteControl)
				return (entity as MyRemoteControl).Pilot as IMyEntity;

			//TODO: Add more pilotable entities
			return entity;
		}

		private void updateFoodLogic()
		{
			//float CurPlayerHealth = -1f;
			bool ChangedStance = false;
			MyObjectBuilder_Character character;
			MyCharacterMovementEnum curmove = MyCharacterMovementEnum.Sitting;

			foreach (IMyPlayer player in mPlayers) {
				
				if (player.Controller != null && player.Controller.ControlledEntity != null && player.Controller.ControlledEntity.Entity != null && player.Controller.ControlledEntity.Entity.DisplayName != "") {

					PlayerData playerData = mPlayerDataStore.get(player);
				    Logging.Instance.WriteLine(playerData.ToString() + "Loaded to Server");

                    IMyEntity entity = player.Controller.ControlledEntity.Entity;
					entity = GetCharacterEntity(entity);
					//					//MyAPIGateway.Utilities.ShowMessage("DEBUG", "Character: " + entity.DisplayName); // gets me player name

					float CurrentModifier = 1f;
					float FatigueRate = 0f;
					
					bool ForceEating = false;
					float RecycleBonus = 1f;
					bool FatigueBonus = false;
					bool HungerBonus = false;
					bool ThirstBonus = false;
					
                   

					// if we were under the effects of a bonus, keep it until we no longer are
					if (playerData.fatigue > MAX_VALUE)
						FatigueBonus = true;
					if (playerData.thirst > MAX_VALUE)
						ThirstBonus = true;
					if (playerData.hunger > MAX_VALUE)
						HungerBonus = true;

					if (entity is IMyCharacter) {
						character = entity.GetObjectBuilder(false) as MyObjectBuilder_Character;
						//MyAPIGateway.Utilities.ShowMessage("DEBUG", "State: " + character.MovementState);
						
						if (playerData.entity == null || playerData.entity.Closed || playerData.entity.EntityId != entity.EntityId) {
							bool bReset = false;

							if (!playerData.loaded) {
								bReset = true;
								playerData.loaded = true;
							}
                            else if ((playerData.entity != null) && (playerData.entity != entity))
                            {
                                bReset = true;
                            }
								
                            // Determines what values you start a new game / playerDataStore with.
                            if (bReset) 
{
								playerData.hunger = STARTING_HUNGER;
								playerData.thirst = STARTING_THIRST;
								playerData.fatigue = STARTING_FATIGUE;
							}
							
							playerData.entity = entity;
						}

                        // Determines what values you re-spawn with.
                        if (dead)
                        {
                            playerData.hunger = RESPAWN_HUNGER;
                            playerData.thirst = RESPAWN_THIRST;
                            playerData.fatigue = RESPAWN_FATIGUE;
                            dead = false;

                        }

                        //MyAPIGateway.Utilities.ShowMessage("DEBUG", "State: " + character.MovementState + ":" + playerData.lastmovement);
                        ChangedStance = playerData.lastmovement != character.MovementState;
						
						curmove = character.MovementState;

						switch (character.MovementState) { // this should be all of them....

							case MyCharacterMovementEnum.Sitting:
								IMyCubeBlock cb = player.Controller.ControlledEntity.Entity as IMyCubeBlock;
								//cb.DisplayNameText is name of individual block, cb.DefinitionDisplayNameText is name of block type
								CurrentModifier = DEFAULT_MODIFIER;
								FatigueRate = FATIGUE_SITTING;
								
								// special case: we may be interacting with a bed, a lunchroom seat or treadmill, so let's
								String seatmodel = cb.DefinitionDisplayNameText.ToLower();
								String seatname = cb.DisplayNameText.ToLower();
								if (seatmodel.Contains("cryo")) // you're in a cryopd not an oxygen bed
								{
									CurrentModifier = 0.0000125f;
									FatigueRate = 0.0000125f;
								} else if (seatmodel.Contains("treadmill")) {
									CurrentModifier = RUNNING_MODIFIER; // jog...
									FatigueRate = FATIGUE_RUNNING/2.5f; // but pace yourself
								} else if (seatmodel.Contains("bed") || seatmodel.Contains("bunk") || seatmodel.Contains("stateroom")) {
									CurrentModifier = DEFAULT_MODIFIER / 2f; // nap time! Needs are reduced.
									FatigueRate = FATIGUE_SITTING * 3f; //  nap time! Rest is greatly sped up.
									FatigueBonus |= !ChangedStance; // longer nap? OK, allow for extra resting
								} else if (seatmodel.Contains("toilet") && ChangedStance) {
									ForceEating = true; // also forces crapping, so this makes sense. but use changedstance to do it only once.
									RecycleBonus = 1.5f;
 								} else if (seatmodel.Contains("bathroom") && ChangedStance) {
									ForceEating = true; // also forces crapping, so this makes sense. but use changedstance to do it only once.
									RecycleBonus = 1.5f;
								} else if (seatname.Contains("noms")) {
									ForceEating = true; // also forces crapping, fortunately the suit takes care of it. Eat continuously while sitting.
									HungerBonus |= playerData.hunger > MAX_VALUE * 0.99; // get to 100% first, then apply bonus.
									ThirstBonus |= playerData.thirst > MAX_VALUE * 0.99; // get to 100% first, then apply bonus.
								}
								break;

							case MyCharacterMovementEnum.Flying:
								CurrentModifier = FLYING_MODIFIER;
								FatigueRate = FATIGUE_FLYING; // operating a jetpack is surprisingly hard
								break;

							case MyCharacterMovementEnum.Falling:
								CurrentModifier = FLYING_MODIFIER;
								FatigueRate = FATIGUE_WALKING; // change nothing for the first iteration (prevents jump exploit)
								if (!ChangedStance)
									FatigueRate = FATIGUE_STANDING; // freefall is actually relaxing when you are used to it. A professional space engineer would be.
								break;
								
							case MyCharacterMovementEnum.Crouching:
							case MyCharacterMovementEnum.CrouchRotatingLeft:
							case MyCharacterMovementEnum.CrouchRotatingRight:
								CurrentModifier = DEFAULT_MODIFIER;
								FatigueRate = FATIGUE_CROUCHING;
								break;

							case MyCharacterMovementEnum.Standing:
							case MyCharacterMovementEnum.RotatingLeft:
							case MyCharacterMovementEnum.RotatingRight:
								CurrentModifier = DEFAULT_MODIFIER;
								FatigueRate = FATIGUE_STANDING;
								break;

							case MyCharacterMovementEnum.CrouchWalking:
							case MyCharacterMovementEnum.CrouchBackWalking:
							case MyCharacterMovementEnum.CrouchStrafingLeft:
							case MyCharacterMovementEnum.CrouchStrafingRight:
							case MyCharacterMovementEnum.CrouchWalkingRightFront:
							case MyCharacterMovementEnum.CrouchWalkingRightBack:
							case MyCharacterMovementEnum.CrouchWalkingLeftFront:
							case MyCharacterMovementEnum.CrouchWalkingLeftBack:
								CurrentModifier = RUNNING_MODIFIER;
								FatigueRate = FATIGUE_RUNNING; // doing the duckwalk is more tiring than walking: try it if you don't believe me
								break;
								
								
								
							case MyCharacterMovementEnum.Walking:
							case MyCharacterMovementEnum.BackWalking:
							case MyCharacterMovementEnum.WalkStrafingLeft:
							case MyCharacterMovementEnum.WalkStrafingRight:
							case MyCharacterMovementEnum.WalkingRightFront:
							case MyCharacterMovementEnum.WalkingRightBack:
							case MyCharacterMovementEnum.WalkingLeftFront:
							case MyCharacterMovementEnum.WalkingLeftBack:
								CurrentModifier = DEFAULT_MODIFIER;
								FatigueRate = FATIGUE_WALKING;
								break;

							case MyCharacterMovementEnum.LadderUp:
								CurrentModifier = RUNNING_MODIFIER;
								FatigueRate = FATIGUE_RUNNING;
								break;
							case MyCharacterMovementEnum.LadderDown:
								CurrentModifier = DEFAULT_MODIFIER;
								FatigueRate = FATIGUE_WALKING;
								break;

							case MyCharacterMovementEnum.Running:
							case MyCharacterMovementEnum.Backrunning:
							case MyCharacterMovementEnum.RunStrafingLeft:
							case MyCharacterMovementEnum.RunStrafingRight:
							case MyCharacterMovementEnum.RunningRightFront:
							case MyCharacterMovementEnum.RunningRightBack:
							case MyCharacterMovementEnum.RunningLeftBack:
							case MyCharacterMovementEnum.RunningLeftFront:
								CurrentModifier = RUNNING_MODIFIER;
								FatigueRate = FATIGUE_RUNNING;
								break;

							case MyCharacterMovementEnum.Sprinting:
							case MyCharacterMovementEnum.Jump:
								CurrentModifier = SPRINTING_MODIFIER;
								FatigueRate = FATIGUE_SPRINTING;
								break;

							case MyCharacterMovementEnum.Died:
								CurrentModifier = DEFAULT_MODIFIER; // unused, but let's have them
								FatigueRate = FATIGUE_STANDING; // unused, but let's have them
								dead = true; // for death recovery logic
								break;

						}
						playerData.lastmovement = character.MovementState; // track delta

					} else if (playerData.entity != null || !playerData.entity.Closed)
						entity = playerData.entity;

					// Sanity checks
					if (HungerBonus) {
						if (playerData.hunger > MAX_VALUE*FOOD_BONUS)
							playerData.hunger = MAX_VALUE*FOOD_BONUS;
					} else {
						if (playerData.hunger > MAX_VALUE)
							playerData.hunger = MAX_VALUE;
					}
					
					if (ThirstBonus) {
						if (playerData.thirst > MAX_VALUE*DRINK_BONUS)
							playerData.thirst = MAX_VALUE*DRINK_BONUS;
					} else {
						if (playerData.thirst > MAX_VALUE)
							playerData.thirst = MAX_VALUE;
					}

					// Cause needs
					if (FATIGUE_ENABLED) {
						playerData.fatigue += (FatigueRate * FOOD_LOGIC_SKIP_TICKS / 60 * 20);// / 15);
						playerData.fatigue = Math.Max(playerData.fatigue, MIN_VALUE);
						if (FatigueBonus)
							playerData.fatigue = Math.Min(playerData.fatigue, MAX_VALUE*REST_BONUS);
						else
							playerData.fatigue = Math.Min(playerData.fatigue, MAX_VALUE);
						
					} else
						playerData.fatigue = 9001f;
					
					if (playerData.fatigue <= 0) {
						
						// fatigue consequences
						// at 0, start causing extra thirst
						// at specified, force walk instead of run (unless overriding by sprinting)
						// at specified, force crouch, and do damage flashes
						// at specified, breathing reflex / mess with helmet, and do a bit of actual damage (just in case thirst isn't already causing it)
						// at specified, cause heart attack

						if (playerData.fatigue <= (0.00f * MIN_VALUE))
						{
							if (EXTRA_THIRST_FROM_FATIGUE > 0)
							{
								// positive: pile on to thirst, per second
								playerData.thirst -= (EXTRA_THIRST_FROM_FATIGUE * FOOD_LOGIC_SKIP_TICKS / 60);
							} else { // negative: multiply modifier
								CurrentModifier *= -EXTRA_THIRST_FROM_FATIGUE;
							}
						}

						if (playerData.fatigue <= (FATIGUE_LEVEL_FORCEWALK * MIN_VALUE))
						{ // force player to walk if they were running
							switch (curmove)
							{
								case MyCharacterMovementEnum.Sprinting:
								case MyCharacterMovementEnum.Running:
								case MyCharacterMovementEnum.Backrunning:
								case MyCharacterMovementEnum.RunStrafingLeft:
								case MyCharacterMovementEnum.RunStrafingRight:
								case MyCharacterMovementEnum.RunningRightFront:
								case MyCharacterMovementEnum.RunningRightBack:
								case MyCharacterMovementEnum.RunningLeftBack:
								case MyCharacterMovementEnum.RunningLeftFront:
									VRage.Game.ModAPI.Interfaces.IMyControllableEntity ce = player.Controller.ControlledEntity.Entity as VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
									ce.SwitchWalk();
									break;
							}
						}

						if (playerData.fatigue <= (FATIGUE_LEVEL_FORCECROUCH * MIN_VALUE))
						{
							bool iscrouching=false;
							switch(curmove)
							{
								case MyCharacterMovementEnum.Crouching:
								case MyCharacterMovementEnum.CrouchWalking:
								case MyCharacterMovementEnum.CrouchBackWalking:
								case MyCharacterMovementEnum.CrouchStrafingLeft:
								case MyCharacterMovementEnum.CrouchStrafingRight:
								case MyCharacterMovementEnum.CrouchWalkingRightFront:
								case MyCharacterMovementEnum.CrouchWalkingRightBack:
								case MyCharacterMovementEnum.CrouchWalkingLeftFront:
								case MyCharacterMovementEnum.CrouchWalkingLeftBack:
									iscrouching=true;
									break;
							}
							if (!iscrouching)
							{
								VRage.Game.ModAPI.Interfaces.IMyControllableEntity ce = player.Controller.ControlledEntity.Entity as VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
								ce.Crouch(); // force player to crouch
							}
						}
						
						if (playerData.fatigue <= (FATIGUE_LEVEL_HELMET * MIN_VALUE))
						{
							VRage.Game.ModAPI.Interfaces.IMyControllableEntity ce = player.Controller.ControlledEntity.Entity as VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
							ce.SwitchHelmet(); // force player to switch helmet, panic reaction from trying to catch breath
							
							var destroyable = entity as IMyDestroyableObject;
							destroyable.DoDamage(0.001f, MyStringHash.GetOrCompute("Fatigue"), true); // starting to hurt
						}

						if (playerData.fatigue <= (FATIGUE_LEVEL_NOHEALING * MIN_VALUE))
						{
							var destroyable = entity as IMyDestroyableObject;
							destroyable.DoDamage(0.001f, MyStringHash.GetOrCompute("Fatigue"), true); // starting to hurt
							if (IsAutohealingOn) // fatigued? no autohealing, either.
							{
								const float HealthTick = 100f / 240f * FOOD_LOGIC_SKIP_TICKS / 60f;
								destroyable.DoDamage(HealthTick, MyStringHash.GetOrCompute("Testing"), false);
							}

						}

						if (playerData.fatigue <= (FATIGUE_LEVEL_HEARTATTACK * MIN_VALUE))
						{
							var destroyable = entity as IMyDestroyableObject;
							destroyable.DoDamage(1000f, MyStringHash.GetOrCompute("Fatigue"), true); // sudden, but very avoidable, heart attack ;)
						}
					}

					// Default Values: 100 (Config value) / 120 (DayLength) / 12 (Amount of 5 seconds) * 1.0f (Current Modifier)
					if (playerData.hunger > MIN_VALUE)
                    {
                        playerData.hunger -= mHungerPerMinute / 10 * CurrentModifier;
                        playerData.hunger = Math.Max(playerData.hunger, MIN_VALUE);
                        MyVisualScriptLogicProvider.SendChatMessage("Hunger: " + playerData.hunger.ToString());
                    }

                    // Default Values: 300 (Config value) / 120 (DayLength) / 12 (Amount of 5 seconds) * 1.0f (Current Modifier)
                    if (playerData.thirst > MIN_VALUE) {
                        playerData.thirst -= mThirstPerMinute / 12  * CurrentModifier;
						playerData.thirst = Math.Max(playerData.thirst, MIN_VALUE);
                        MyVisualScriptLogicProvider.SendChatMessage("Thirst: " + playerData.thirst.ToString());
                    }

					// Try to meet needs
					if (playerData.hunger < (MAX_VALUE * HUNGRY_WHEN) || ForceEating)
						playerEatSomething(entity, playerData, HungerBonus?MAX_VALUE*1.25f:MAX_VALUE,RecycleBonus);
					
					if (playerData.thirst < (MAX_VALUE * THIRSTY_WHEN) || ForceEating)
						playerDrinkSomething(entity, playerData, ThirstBonus?MAX_VALUE*1.25f:MAX_VALUE,RecycleBonus);

					// Cause damage if needs are unmet
					if (playerData.thirst <= 0) {
						var destroyable = entity as IMyDestroyableObject;
						if (DAMAGE_SPEED_THIRST > 0)
							destroyable.DoDamage((IsAutohealingOn ? (DAMAGE_SPEED_THIRST + 1f) : DAMAGE_SPEED_THIRST), MyStringHash.GetOrCompute("Thirst"), true);
						else
							destroyable.DoDamage(((IsAutohealingOn ? (-DAMAGE_SPEED_THIRST + 1f) : -DAMAGE_SPEED_THIRST) + DAMAGE_SPEED_THIRST * playerData.thirst), MyStringHash.GetOrCompute("Thirst"), true);
					}

					if (playerData.hunger <= 0) {
						var destroyable = entity as IMyDestroyableObject;
						if (DAMAGE_SPEED_HUNGER > 0)
							destroyable.DoDamage((IsAutohealingOn ? (DAMAGE_SPEED_HUNGER + 1f) : DAMAGE_SPEED_HUNGER), MyStringHash.GetOrCompute("Hunger"), true);
						else
							destroyable.DoDamage(((IsAutohealingOn ? (-DAMAGE_SPEED_HUNGER + 1f) : -DAMAGE_SPEED_HUNGER) + DAMAGE_SPEED_HUNGER * playerData.hunger), MyStringHash.GetOrCompute("Hunger"), true);
					}



					/*
					character = entity.GetObjectBuilder(false) as MyObjectBuilder_Character;
					if (character.Health == null) // ok, so the variable exists, but it's always null for some reason?
						CurPlayerHealth = 101f;
					else
						CurPlayerHealth = (float) (character.Health);

					if (IsAutohealingOn && CurPlayerHealth < 70f)
					{
						const float HealthTick = 100f / 240f * FOOD_LOGIC_SKIP_TICKS / 60f;
						var destroyable = entity as IMyDestroyableObject;
						destroyable.DoDamage(HealthTick, MyStringHash.GetOrCompute("Testing"), false);
					}
					 */

					if (dead && DEATH_RECOVERY > 0.0) {
						MyInventoryBase inventory = ((MyEntity)entity).GetInventoryBase();
						if (playerData.hunger > 0)
							inventory.AddItems((MyFixedPoint)((1f / MAX_VALUE) * DEATH_RECOVERY * (playerData.hunger)), new MyObjectBuilder_Ore() { SubtypeName = "Organic" });
						if (playerData.thirst > 0)
							inventory.AddItems((MyFixedPoint)((1f / MAX_VALUE) * DEATH_RECOVERY * (playerData.thirst)), new MyObjectBuilder_Ingot() { SubtypeName = "GreyWater" });
					}

				    //Sends data from Server.cs to Client.cs
				    string message = MyAPIGateway.Utilities.SerializeToXML<PlayerData>(playerData);
                    Logging.Instance.WriteLine(("Message sent from Server.cs to Client.cs: " + message));
                    MyAPIGateway.Multiplayer.SendMessageTo(
						1337,
						Encoding.Unicode.GetBytes(message),
						player.SteamUserId
					);
                }
			}
		}

		public void AdminCommandHandler(byte[] data)
		{
			//Keen why do you not pass the steamId? :/
			Command command = MyAPIGateway.Utilities.SerializeFromXML<Command>(Encoding.Unicode.GetString(data));

			/*if (Utils.isAdmin(command.sender)) {
                var words = command.content.Trim().ToLower().Replace("/", "").Split(' ');
                if (words.Length > 0 && words[0] == "hatm") {
                    switch (words[1])
                    {
                        case "blacklist":
                            IMyPlayer player = mPlayers.Find(p => words[2] == p.DisplayName);
                            mConfig.BlacklistAdd(player.SteamUserId);
                            break;
                    }
                }
            }*/
		}

		public void NeedsApiHandler(object data)
		{
			//mFoodTypes.Add(szItemName, hungerValue);
			//mBeverageTypes.Add(szItemName, thirstValue);

			NeedsApi.Event e = (NeedsApi.Event)data;

			if (e.type == NeedsApi.Event.Type.RegisterEdibleItem) {
				NeedsApi.RegisterEdibleItemEvent edibleItemEvent = (NeedsApi.RegisterEdibleItemEvent)e.payload;
				//MyAPIGateway.Utilities.ShowMessage("DEBUG", "EdibleItem " + edibleItemEvent.item + "(" +  edibleItemEvent.value + ") registered");
				mFoodTypes.Add(edibleItemEvent.item, edibleItemEvent.value);
			} else if (e.type == NeedsApi.Event.Type.RegisterDrinkableItem) {
				NeedsApi.RegisterDrinkableItemEvent drinkableItemEvent = (NeedsApi.RegisterDrinkableItemEvent)e.payload;
				//MyAPIGateway.Utilities.ShowMessage("DEBUG", "DrinkableItem " + drinkableItemEvent.item + "(" +  drinkableItemEvent.value + ") registered");
				mBeverageTypes.Add(drinkableItemEvent.item, drinkableItemEvent.value);
			}
		}

		public override void UpdateAfterSimulation()
		{
			if (MyAPIGateway.Session == null)
				return;

			// Food logic is desactivated in creative mode

			if (MyAPIGateway.Session.SessionSettings.GameMode == MyGameModeEnum.Creative)
				return;

			try {
				if (MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer) {
					if (!mStarted) {
						mStarted = true;
						init();

						food_logic_skip = FOOD_LOGIC_SKIP_TICKS;
					}

					if (++food_logic_skip >= FOOD_LOGIC_SKIP_TICKS) {
						food_logic_skip = 0;

						updatePlayerList();
						updateFoodLogic();
					}
				}
			}
			catch (Exception e)
			{
                //MyApiGateway.Utilities.ShowMessage("ERROR", "Logger error: " + e.Message + "\n" + e.StackTrace);
                
                Logging.Instance.WriteLine(("(FoodSystem) Server UpdateSimulation Error: " + e.Message + "\n" + e.StackTrace));
            }
		}

		// Saving datas when requested

		public override void SaveData()
		{
			mPlayerDataStore.Save();
			mConfigDataStore.Save();
		}

		protected override void UnloadData()
		{
			mStarted = false;
			MyAPIGateway.Multiplayer.UnregisterMessageHandler(1338, AdminCommandHandler);
			MyAPIGateway.Utilities.UnregisterMessageHandler(1339, NeedsApiHandler);
			mPlayers.Clear();
			mFoodTypes.Clear();
			mBeverageTypes.Clear();
			mPlayerDataStore.clear();
			mConfigDataStore.clear();
			Logging.Instance.Close();
		}
	}
}