using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ImpactfulSkills.common {
    internal static class DataObjects {

        [Serializable]
        internal class XPIncreaseRequest {
            // The largest range a grant is allowed to cover. Requests arrive from clients, and the server
            // turns the range into a peer lookup, so an unbounded value would let one packet fan out to
            // everyone on the server. The widest range any config exposes is 100.
            internal const float MaxRange = 100f;

            public SerializableVector3 Location { get; set;}
            public float Range { get; set;}
            public Skills.SkillType Skill { get; set; }
            public float Amount { get; set; }

            /// <summary>
            /// Writes the request field by field. BinaryFormatter is deliberately not used here: it stamps
            /// the mods assembly version into the payload, so a server and client on different builds could
            /// never read each others packages, and it reconstructs arbitrary types straight off the wire.
            /// </summary>
            internal ZPackage ToPackage() {
                ZPackage pkg = new ZPackage();
                pkg.Write(Location.x);
                pkg.Write(Location.y);
                pkg.Write(Location.z);
                pkg.Write(Range);
                pkg.Write((int)Skill);
                pkg.Write(Amount);
                return pkg;
            }

            /// <summary>
            /// Reads a request written by <see cref="ToPackage"/>, clamping the values that a remote peer
            /// controls. Reads from the start of the package, so the caller does not have to care whether
            /// anything has already read from it.
            /// </summary>
            internal static XPIncreaseRequest FromPackage(ZPackage pkg) {
                pkg.SetPos(0);
                XPIncreaseRequest request = new XPIncreaseRequest();
                request.Location = new SerializableVector3(pkg.ReadSingle(), pkg.ReadSingle(), pkg.ReadSingle());
                request.Range = Mathf.Clamp(pkg.ReadSingle(), 0f, MaxRange);
                request.Skill = (Skills.SkillType)pkg.ReadInt();
                request.Amount = pkg.ReadSingle();
                return request;
            }
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
