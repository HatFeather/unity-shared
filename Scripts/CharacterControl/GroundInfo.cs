using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HatFeather.Shared.CharacterControl
{
    public struct GroundInfo
    {
        public Transform transform;
        public Vector3 normal;
        public Vector3 contact;
        public Vector3 orthogonal;
        public float slope;
        public string tag;

        public bool containsInfo => slope != float.NaN;
        public int mask => transform.gameObject.layer;

        public void clearInfo()
        {
            transform = null;
            normal.Set(0, 0, 0);
            contact.Set(0, 0, 0);
            orthogonal.Set(0, 0, 0);
            slope = float.NaN;
            tag = string.Empty;
        }
    }
}
