using VRage.ModAPI;
using VRage.Game;
using System.Xml.Serialization;

namespace Stollie.DailyNeeds {
    public class PlayerData
    {
        public ulong steamid;
        public float hunger;
        public float thirst;
        public float fatigue;
        public bool dead;

        [XmlIgnoreAttribute]
        public VRage.Game.MyCharacterMovementEnum lastmovement;
        
        [XmlIgnoreAttribute]
        public IMyEntity entity;
        
        [XmlIgnoreAttribute]
        public bool loaded;

        public PlayerData(ulong id)
        {
            thirst = 100;
            hunger = 100;
            fatigue = 100;
            lastmovement = 0;
            dead = false;
            entity = null;
            steamid = id;
            loaded = false;
        }

        public PlayerData() {
            thirst = 100;
            hunger = 100;
            fatigue = 100;
            lastmovement = 0;
            dead = false;
            entity = null;
            loaded = false;
        }
    }
}
