using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HatFeather.Shared.CharacterControl
{
    [System.Serializable]
    public class FirstPersonMotionInput
    {
        public InputWrapper jump = "Jump";
        public InputWrapper crouch = "Crouch";
        public InputWrapper run = "Run";
        public InputWrapper horizontal = "Horizontal";
        public InputWrapper vertical = "Vertical";
    }
}
