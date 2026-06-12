using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ImpactfulSkills.common {
    internal static class DataObjects {

        public static BinaryFormatter binFormatter = new BinaryFormatter();

        [Serializable]
        internal class XPIncreaseRequest {
            public SerializableVector3 Location { get; set;}
            public float Range { get; set;}
            public Skills.SkillType Skill { get; set; }
            public float Amount { get; set; }
        }

        [Serializable]
        public struct SerializableVector3 {
            public float x;
            public float y;
            public float z;

            public SerializableVector3(float rX, float rY, float rZ) {
                x = rX;
                y = rY;
                z = rZ;
            }

            public override string ToString() {
                return String.Format("[{0}, {1}, {2}]", x, y, z);
            }

            public static implicit operator Vector3(SerializableVector3 rValue) {
                return new Vector3(rValue.x, rValue.y, rValue.z);
            }

            public static implicit operator SerializableVector3(Vector3 rValue) {
                return new SerializableVector3(rValue.x, rValue.y, rValue.z);
            }
        }

        public abstract class ZNetProperty<T> {
            public string Key {
                get; private set;
            }
            public T DefaultValue {
                get; private set;
            }
            protected readonly ZNetView zNetView;

            protected ZNetProperty(string key, ZNetView zNetView, T defaultValue) {
                Key = key;
                DefaultValue = defaultValue;
                this.zNetView = zNetView;
            }

            private void ClaimOwnership() {
                if (!zNetView.IsOwner()) {
                    zNetView.ClaimOwnership();
                }
            }

            public void Set(T value) {
                SetValue(value);
            }

            public void ForceSet(T value) {
                ClaimOwnership();
                Set(value);
            }

            public abstract T Get();

            protected abstract void SetValue(T value);
        }
    }
}
