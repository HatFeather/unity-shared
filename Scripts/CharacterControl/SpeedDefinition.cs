using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HatFeather.Shared.CharacterControl
{
    [System.Serializable]
    public struct SpeedDefinition
    {
        public float running;
        public float walking;
        public float crouched;

        public static SpeedDefinition Default
        {
            get
            {
                SpeedDefinition ret = new SpeedDefinition();
                ret.running = 6.5f;
                ret.walking = 4f;
                ret.crouched = 2f;
                return ret;
            }
        }
    }
}
