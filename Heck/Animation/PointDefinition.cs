using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Heck.Animation
{
    public class PointDefinition
    {
        private readonly List<PointData> _points;

        public PointDefinition()
            : this(new List<PointData>())
        {
        }

        private PointDefinition(List<PointData> points)
        {
            _points = points;
        }

        public static PointDefinition ListToPointDefinition(List<object> list)
        {
            // converts [...] to [[..., 0]]
            IEnumerable<List<object>> points = list.FirstOrDefault() is List<object> ? list.Cast<List<object>>() : new[] { list.Append(0).ToList() };

            List<PointData> pointData = new();
            foreach (List<object> rawPoint in points)
            {
                // Get all numbers
                List<float> numbers = rawPoint.SelectNonNull<object, float>(s =>
                {
                    try
                    {
                        return Convert.ToSingle(s);
                    }
                    catch (FormatException)
                    {
                        return null;
                    }
                }).ToList();

                // If no numbers, do nothing
                if (numbers.Count == 0)
                {
                    continue;
                }

                float time = numbers.Last();

                Functions easing = Functions.easeLinear;
                bool spline = false;
                List<string> flags = rawPoint.SafeCast<string>().ToList();

                string? easingString = (string?)flags.FirstOrDefault(s => s.StartsWith("ease"));

                if (easingString != null)
                {
                    easing = (Functions)Enum.Parse(typeof(Functions), easingString);
                }

                string? splineString = flags.FirstOrDefault(n => n.StartsWith("spline"));
                if (splineString == "splineCatmullRom")
                {
                    spline = true;
                }

                List<float> datas = numbers.GetRange(0, numbers.Count - 1);
                if (datas.Count == numbers.Count)
                {
                    throw new InvalidOperationException();
                }

                pointData.Add(new PointData(datas.ToArray(), time, easing, spline));
            }

            return new PointDefinition(pointData);
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new("{ ");
            _points.ForEach(n => stringBuilder.Append($"{string.Join(", ", n.PointDatas)}:{n.Time} "));

            stringBuilder.Append('}');
            return stringBuilder.ToString();
        }

        public Vector3 Interpolate(float time)
        {
            if (InterpolateRaw(time, out PointData? pointL, out PointData? pointR, out float normalTime, out int l, out int r))
            {
                return pointR!.Smooth ? SmoothVectorLerp(_points, l, r, normalTime)
                    : Vector3.LerpUnclamped(pointL!.ToVector3(), pointR.ToVector3(), normalTime);
            }

            return pointL?.ToVector3() ?? Vector3.zero;
        }

        public Quaternion InterpolateQuaternion(float time)
        {
            // ReSharper disable once InvertIf
            if (InterpolateRaw(time, out PointData? pointL, out PointData? pointR, out float normalTime, out int _, out int _))
            {
                Quaternion quaternionOne = Quaternion.Euler(pointL!.ToVector3());
                Quaternion quaternionTwo = Quaternion.Euler(pointR!.ToVector3());

                return Quaternion.SlerpUnclamped(quaternionOne, quaternionTwo, normalTime);
            }

            return pointL != null ? Quaternion.Euler(pointL.ToVector3()) : Quaternion.identity;
        }

        // Kind of a sloppy way of implementing this, but hell if it works
        public float InterpolateLinear(float time)
        {
            if (InterpolateRaw(time, out PointData? pointL, out PointData? pointR, out float normalTime, out int _, out int _))
            {
                return Mathf.LerpUnclamped(pointL!.ToFloat(), pointR!.ToFloat(), normalTime);
            }

            return pointL?.ToFloat() ?? 0;
        }

        public Vector4 InterpolateVector4(float time)
        {
            if (InterpolateRaw(time, out PointData? pointL, out PointData? pointR, out float normalTime, out int _, out int _))
            {
                return Vector4.LerpUnclamped(pointL!.ToVector4(), pointR!.ToVector4(), normalTime);
            }

            return pointL?.ToVector4() ?? Vector4.zero;
        }

        private static Vector3 SmoothVectorLerp(List<PointData> points, int a, int b, float time)
        {
            // Catmull-Rom Spline
            Vector3 p0 = a - 1 < 0 ? points[a].ToVector3() : points[a - 1].ToVector3();
            Vector3 p1 = points[a].ToVector3();
            Vector3 p2 = points[b].ToVector3();
            Vector3 p3 = b + 1 > points.Count - 1 ? points[b].ToVector3() : points[b + 1].ToVector3();

            float tt = time * time;
            float ttt = tt * time;

            float q0 = -ttt + (2.0f * tt) - time;
            float q1 = (3.0f * ttt) - (5.0f * tt) + 2.0f;
            float q2 = (-3.0f * ttt) + (4.0f * tt) + time;
            float q3 = ttt - tt;

            Vector3 c = 0.5f * ((p0 * q0) + (p1 * q1) + (p2 * q2) + (p3 * q3));

            return c;
        }



        /// <summary>
        /// Does most of the interpolation magic between points
        /// </summary>
        /// <param name="time">time.</param>
        /// <param name="pointL">If returned false, will be the point with data and no interpolation. If true, will interpolate to pointR in normalTime.</param>
        /// <param name="pointR">If returned true, will interpolate from pointL to pointR in normalTime.</param>
        /// <param name="normalTime">interpolation time.</param>
        /// <param name="l">left value index.</param>
        /// <param name="r">right value index.</param>
        /// <returns>True if not interpolating between two values.</returns>
        private bool InterpolateRaw(float time, out PointData? pointL, out PointData? pointR, out float normalTime, out int l, out int r)
        {
            // Idk why I called this InterpolateRaw. Whatever
            pointL = null;
            pointR = null;
            normalTime = 0;
            l = 0;
            r = 0;

            if (_points.Count == 0)
            {
                return false;
            }

            PointData first = _points.First();
            if (first.Time >= time)
            {
                pointL = first;
                return false;
            }

            PointData last = _points.Last();
            if (last.Time <= time)
            {
                pointL = last;
                return false;
            }

            SearchIndex(time, out l, out r);
            pointL = _points[l];
            pointR = _points[r];

            float divisor = pointR.Time - pointL.Time;
            normalTime = divisor != 0 ? (time - pointL.Time) / divisor : 0;
            normalTime = Easings.Interpolate(normalTime, pointR.Easing);

            return true;
        }

        // Use binary search instead of linear search.
        private void SearchIndex(float time, out int l, out int r)
        {
            l = 0;
            r = _points.Count;

            while (l < r - 1)
            {
                int m = (l + r) / 2;
                float pointTime = _points[m].Time;

                if (pointTime < time)
                {
                    l = m;
                }
                else
                {
                    r = m;
                }
            }
        }


        private class PointData
        {
            internal PointData(float[] pointDatas, float time, Functions easing = Functions.easeLinear, bool smooth = false)
            {
                PointDatas = pointDatas;
                Time = time;
                Easing = easing;
                Smooth = smooth;
            }

            internal float[] PointDatas { get; }

            internal float Time { get; }

            internal Functions Easing { get; }

            internal bool Smooth { get; }

            internal Vector3 ToVector3() => new(PointDatas[0], PointDatas[1], PointDatas[2]);

            internal Vector4 ToVector4() => new(PointDatas[0], PointDatas[1], PointDatas[2], PointDatas[3]);

            internal float ToFloat() => PointDatas[0];
        }
    }
}
