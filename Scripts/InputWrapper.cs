using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HatFeather.Shared
{
    [System.Serializable]
    public struct InputWrapper
    {
        public string inputName;

        public InputWrapper(string inputName)
        {
            this.inputName = inputName;
        }

        public static implicit operator string(InputWrapper wrapper) => wrapper.inputName;
        public static implicit operator InputWrapper(string name) => new InputWrapper(name);

        public float Axis => Input.GetAxis(inputName);
        public float AxisRaw => Input.GetAxisRaw(inputName);

        public bool PressedDown => Input.GetButtonDown(inputName);
        public bool IsHeld => Input.GetButton(inputName);
        public bool Released => Input.GetButtonUp(inputName);
    }
}
