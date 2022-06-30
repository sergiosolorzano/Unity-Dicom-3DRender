using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//namespace Rendergon_DicomMethods.Dicom.Math
namespace Rendergon.Dicom
{
    public class QuaternionOrder
    {
        public enum RotSeq
        {
            zyx, zyz, zxy, zxz, yxz, yxy, yzx, yzy, xyz, xyx, xzy, xzx
        };

        static Vector3 Twoaxisrot(float r11, float r12, float r21, float r31, float r32)
        {
            Vector3 ret = new Vector3();
            ret.x = Mathf.Atan2(r11, r12);
            ret.y = Mathf.Acos(r21);
            ret.z = Mathf.Atan2(r31, r32);
            return ret;
        }

        static Vector3 Threeaxisrot(float r11, float r12, float r21, float r31, float r32)
        {
            Vector3 ret = new Vector3();
            ret.x = Mathf.Atan2(r31, r32);
            ret.y = Mathf.Asin(r21);
            ret.z = Mathf.Atan2(r11, r12);
            return ret;
        }

        public static Vector3 Quaternion2Euler(Quaternion q, RotSeq rotSeq)
        {
            switch (rotSeq)
            {
                case RotSeq.zyx:
                    return Threeaxisrot(2 * (q.x * q.y + q.w * q.z),
                        q.w * q.w + q.x * q.x - q.y * q.y - q.z * q.z,
                        -2 * (q.x * q.z - q.w * q.y),
                        2 * (q.y * q.z + q.w * q.x),
                        q.w * q.w - q.x * q.x - q.y * q.y + q.z * q.z);


                case RotSeq.zyz:
                    return Twoaxisrot(2 * (q.y * q.z - q.w * q.x),
                        2 * (q.x * q.z + q.w * q.y),
                        q.w * q.w - q.x * q.x - q.y * q.y + q.z * q.z,
                        2 * (q.y * q.z + q.w * q.x),
                        -2 * (q.x * q.z - q.w * q.y));


                case RotSeq.zxy:
                    return Threeaxisrot(-2 * (q.x * q.y - q.w * q.z),
                        q.w * q.w - q.x * q.x + q.y * q.y - q.z * q.z,
                        2 * (q.y * q.z + q.w * q.x),
                        -2 * (q.x * q.z - q.w * q.y),
                        q.w * q.w - q.x * q.x - q.y * q.y + q.z * q.z);


                case RotSeq.zxz:
                    return Twoaxisrot(2 * (q.x * q.z + q.w * q.y),
                        -2 * (q.y * q.z - q.w * q.x),
                        q.w * q.w - q.x * q.x - q.y * q.y + q.z * q.z,
                        2 * (q.x * q.z - q.w * q.y),
                        2 * (q.y * q.z + q.w * q.x));


                case RotSeq.yxz:
                    return Threeaxisrot(2 * (q.x * q.z + q.w * q.y),
                        q.w * q.w - q.x * q.x - q.y * q.y + q.z * q.z,
                        -2 * (q.y * q.z - q.w * q.x),
                        2 * (q.x * q.y + q.w * q.z),
                        q.w * q.w - q.x * q.x + q.y * q.y - q.z * q.z);

                case RotSeq.yxy:
                    return Twoaxisrot(2 * (q.x * q.y - q.w * q.z),
                        2 * (q.y * q.z + q.w * q.x),
                        q.w * q.w - q.x * q.x + q.y * q.y - q.z * q.z,
                        2 * (q.x * q.y + q.w * q.z),
                        -2 * (q.y * q.z - q.w * q.x));


                case RotSeq.yzx:
                    return Threeaxisrot(-2 * (q.x * q.z - q.w * q.y),
                        q.w * q.w + q.x * q.x - q.y * q.y - q.z * q.z,
                        2 * (q.x * q.y + q.w * q.z),
                        -2 * (q.y * q.z - q.w * q.x),
                        q.w * q.w - q.x * q.x + q.y * q.y - q.z * q.z);


                case RotSeq.yzy:
                    return Twoaxisrot(2 * (q.y * q.z + q.w * q.x),
                        -2 * (q.x * q.y - q.w * q.z),
                        q.w * q.w - q.x * q.x + q.y * q.y - q.z * q.z,
                        2 * (q.y * q.z - q.w * q.x),
                        2 * (q.x * q.y + q.w * q.z));


                case RotSeq.xyz:
                    return Threeaxisrot(-2 * (q.y * q.z - q.w * q.x),
                        q.w * q.w - q.x * q.x - q.y * q.y + q.z * q.z,
                        2 * (q.x * q.z + q.w * q.y),
                        -2 * (q.x * q.y - q.w * q.z),
                        q.w * q.w + q.x * q.x - q.y * q.y - q.z * q.z);


                case RotSeq.xyx:
                    return Twoaxisrot(2 * (q.x * q.y + q.w * q.z),
                        -2 * (q.x * q.z - q.w * q.y),
                        q.w * q.w + q.x * q.x - q.y * q.y - q.z * q.z,
                        2 * (q.x * q.y - q.w * q.z),
                        2 * (q.x * q.z + q.w * q.y));


                case RotSeq.xzy:
                    return Threeaxisrot(2 * (q.y * q.z + q.w * q.x),
                        q.w * q.w - q.x * q.x + q.y * q.y - q.z * q.z,
                        -2 * (q.x * q.y - q.w * q.z),
                        2 * (q.x * q.z + q.w * q.y),
                        q.w * q.w + q.x * q.x - q.y * q.y - q.z * q.z);


                case RotSeq.xzx:
                    return Twoaxisrot(2 * (q.x * q.z - q.w * q.y),
                        2 * (q.x * q.y + q.w * q.z),
                        q.w * q.w + q.x * q.x - q.y * q.y - q.z * q.z,
                        2 * (q.x * q.z + q.w * q.y),
                        -2 * (q.x * q.y - q.w * q.z));

                default:
                    Debug.LogError("No good sequence");
                    return Vector3.zero;

            }
        }
    }
}