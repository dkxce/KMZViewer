//#define gpc
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;

#if gpc
using GpcWrapper;
#endif


namespace PolyLineBufferSimple
{
    public class PolyLineBufferCrsimp
    {
        public class PolyResult
        {
            public PointF[] polygon;
            public List<PointF[]> segments;

            public PolyResult()
            {
                this.polygon = new PointF[0];
                this.segments = new List<PointF[]>();
            }

            public PolyResult(PointF[] polygon, List<PointF[]> segments)
            {
                this.polygon = polygon;
                this.segments = segments;
            }

            public bool PointIn(PointF point)
            {
                if (segments.Count == 0) return false;
                for (int i = 0; i < segments.Count; i++)
                    if (PointInPolygon(point, segments[i]))
                        return true;
                return false;
            }

            private static bool PointInPolygon(PointF point, PointF[] polygon)
            {
                if (polygon == null) return false;
                if (polygon.Length < 2) return false;

                int i, j, nvert = polygon.Length;
                bool c = false;

                for (i = 0, j = nvert - 1; i < nvert; j = i++)
                {
                    if (((polygon[i].Y >= point.Y) != (polygon[j].Y >= point.Y)) &&
                        (point.X <= (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                      )
                        c = !c;
                }

                return c;
            }
        }

        /// <summary>
        ///     Calc distance in custom units
        /// </summary>
        /// <param name="a">Point A</param>
        /// <param name="b">Point B</param>
        /// <returns>distance in custom units</returns>
        public delegate float DistanceFunction(PointF a, PointF b);

        /// <summary>
        ///     return (float)Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static float SampleDistFunc(PointF a, PointF b)
        {
            return (float)Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        }

        /// <summary>
        ///     return distance in meters between 2 points
        /// </summary>
        /// <param name="a">Point A</param>
        /// <param name="b">Point B</param>
        /// <returns>distance in meters</returns>
        public static float GeographicDistFunc(PointF a, PointF b)
        {
            return GetGeoLengthInMetersC(a.Y, a.X, b.Y, b.X, false);
        }

        /// <summary>
        ///     Return total length of polyline in meters
        /// </summary>
        /// <param name="polyline"></param>
        /// <returns>in meters</returns>
        public static uint GetDistInMeters(PointF[] polyline, bool polygon)
        {
            if (polyline == null) return 0;
            if (polyline.Length < 2) return 0;
            uint res = 0;
            for (int i = 1; i < polyline.Length; i++)
                res += GetGeoLengthInMetersC(polyline[i - 1].Y, polyline[i - 1].X, polyline[i].Y, polyline[i].X, false);
            if(polygon)
                res += GetGeoLengthInMetersC(polyline[polyline.Length - 1].Y, polyline[polyline.Length - 1].X, polyline[0].Y, polyline[0].X, false);
            return res;
        }

        private static double GetDeterminant(double x1, double y1, double x2, double y2)
        {
            return x1 * y2 - x2 * y1;
        }

        /// <summary>
        ///     Calculate Square of Geographic Polygon By Simplify Method
        ///     (faster)
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public static double GetSquareInMetersA(PointF[] poly)
        {
            if (poly == null) return 0;
            if (poly.Length < 3) return 0;
            PointF st = new PointF(float.MaxValue, float.MaxValue);
            for (int i = 0; i < poly.Length; i++)
            {
                if (poly[i].X < st.X) st.X = poly[i].X;
                if (poly[i].Y < st.Y) st.Y = poly[i].Y;
            };
            PointF[] polygon = new PointF[poly.Length];
            for (int i = 0; i < polygon.Length; i++)
                polygon[i] = new PointF(GetGeoLengthInMetersC(st.Y, st.X, st.Y, poly[i].X, false), GetGeoLengthInMetersC(st.Y, st.X, poly[i].Y, st.X, false));

            double area = GetDeterminant(polygon[polygon.Length - 1].X, polygon[polygon.Length - 1].Y, polygon[0].X, polygon[0].Y);
            for (int i = 1; i < polygon.Length; i++)
                area += GetDeterminant(polygon[i - 1].X, polygon[i - 1].Y, polygon[i].X, polygon[i].Y);

            return Math.Abs(area / 2.0 / 1000000.0);
        }
    
        /// <summary>
        ///     Calculate Square of Geographic Polygon
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public static double GetSquareInMeters(PointF[] poly)
        {
            return GetSquareInMetersA(poly);
        }

        /// <summary>
        ///     Geographic Get Distance Between 2 points
        /// </summary>
        /// <param name="StartLat">A Lat</param>
        /// <param name="StartLong">A Lon</param>
        /// <param name="EndLat">B Lat</param>
        /// <param name="EndLong">B Lon</param>
        /// <param name="radians">radians or degrees</param>
        /// <returns>length in meters</returns>
        public static uint GetGeoLengthInMetersC(double StartLat, double StartLong, double EndLat, double EndLong, bool radians)
        {
            double D2R = Math.PI / 180;
            if (radians) D2R = 1;
            double dDistance = Double.MinValue;
            double dLat1InRad = StartLat * D2R;
            double dLong1InRad = StartLong * D2R;
            double dLat2InRad = EndLat * D2R;
            double dLong2InRad = EndLong * D2R;

            double dLongitude = dLong2InRad - dLong1InRad;
            double dLatitude = dLat2InRad - dLat1InRad;

            // Intermediate result a.
            double a = Math.Pow(Math.Sin(dLatitude / 2.0), 2.0) +
                       Math.Cos(dLat1InRad) * Math.Cos(dLat2InRad) *
                       Math.Pow(Math.Sin(dLongitude / 2.0), 2.0);

            // Intermediate result c (great circle distance in Radians).
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            const double kEarthRadiusKms = 6378137.0000;
            dDistance = kEarthRadiusKms * c;

            return (uint)Math.Round(dDistance);
        }

        public static PointF LineIntersection(PointF A, PointF B, PointF C, PointF D)
        {
            // Line AB represented as a1x + b1y = c1  
            double a1 = B.Y - A.Y;
            double b1 = A.X - B.X;
            double c1 = a1 * (A.X) + b1 * (A.Y);

            // Line CD represented as a2x + b2y = c2  
            double a2 = D.Y - C.Y;
            double b2 = C.X - D.X;
            double c2 = a2 * (C.X) + b2 * (C.Y);

            double determinant = a1 * b2 - a2 * b1;

            if (determinant == 0)
            {
                // The lines are parallel. This is simplified  
                // by returning a pair of FLT_MAX  
                return new PointF((float)double.MaxValue, (float)double.MaxValue);
            }
            else
            {
                double x = (b2 * c1 - b1 * c2) / determinant;
                double y = (a1 * c2 - a2 * c1) / determinant;
                return new PointF((float)x, (float)y);
            }
        }

        private static bool IsInsideLine(PointF[] line, double x, double y)
        {
            return (x >= line[0].X && x <= line[1].X
                        || x >= line[1].X && x <= line[0].X)
                   && (y >= line[0].Y && y <= line[1].Y
                        || y >= line[1].Y && y <= line[0].Y);
        }

        private static bool IsInsideLine(PointF[] line, PointF point)
        {
            return IsInsideLine(line, point.X, point.Y);
        }

        private static bool IsInsideLine(PointF lineA , PointF lineB, PointF point)
        {
            return IsInsideLine(new PointF[] { lineA, lineB }, point.X, point.Y);
        }       
        
        /// <summary>
        ///     Distance from specified point to line
        /// </summary>
        /// <param name="pt">Specified point</param>
        /// <param name="lineStart">Line Start</param>
        /// <param name="lineEnd">Line End</param>
        /// <param name="DistanceFunc">Get Distance Function</param>
        /// <param name="pointOnLine">Nearest point on line</param>
        /// <param name="side">side of</param>
        /// <returns>distance</returns>
        public static float DistanceFromPointToLine(PointF pt, PointF lineStart, PointF lineEnd, DistanceFunction DistanceFunc, out PointF pointOnLine, out int side)
        {
            float dx = lineEnd.X - lineStart.X;
            float dy = lineEnd.Y - lineStart.Y;

            if ((dx == 0) && (dy == 0))
            {
                // line is a point
                // линия может быть с нулевой длиной после анализа TRA
                pointOnLine = lineStart;
                side = 0;
                //dx = pt.X - lineStart.X;
                //dy = pt.Y - lineStart.Y;                
                //return Math.Sqrt(dx * dx + dy * dy);
                float dist = DistanceFunc == null ? SampleDistFunc(pt, pointOnLine) : DistanceFunc(pt, pointOnLine);
                return dist;
            };

            side = Math.Sign((lineEnd.X - lineStart.X) * (pt.Y - lineStart.Y) - (lineEnd.Y - lineStart.Y) * (pt.X - lineStart.X));

            // Calculate the t that minimizes the distance.
            float t = ((pt.X - lineStart.X) * dx + (pt.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);

            // See if this represents one of the segment's
            // end points or a point in the middle.
            if (t < 0)
            {
                pointOnLine = new PointF(lineStart.X, lineStart.Y);
                dx = pt.X - lineStart.X;
                dy = pt.Y - lineStart.Y;
            }
            else if (t > 1)
            {
                pointOnLine = new PointF(lineEnd.X, lineEnd.Y);
                dx = pt.X - lineEnd.X;
                dy = pt.Y - lineEnd.Y;
            }
            else
            {
                pointOnLine = new PointF(lineStart.X + t * dx, lineStart.Y + t * dy);
                dx = pt.X - pointOnLine.X;
                dy = pt.Y - pointOnLine.Y;
            };

            float d = DistanceFunc == null ? SampleDistFunc(pt, pointOnLine) : DistanceFunc(pt, pointOnLine);
            return d;
        }

        /// <summary>
        ///     Get Distance by Route
        /// </summary>
        /// <param name="pt">Point</param>
        /// <param name="route">Route</param>
        /// <param name="DistanceFunc">Dist Func</param>
        /// <param name="distance_from_start">Distance from Start of the Route to Point</param>
        /// <returns>Distance from Point to Route</returns>
        public static float DistanceFromPointToRoute(PointF pt, PointF[] route, DistanceFunction DistanceFunc, out float distance_from_start)
        {
            float route_dist = 0;
            float min_dist = float.MaxValue;
            distance_from_start = float.MaxValue;
            for (int i = 1; i < route.Length; i++)
            {
                PointF pointOnLine = PointF.Empty; int side = 0;
                float dist2line = DistanceFromPointToLine(pt, route[i - 1], route[i], DistanceFunc, out pointOnLine, out side);
                if (dist2line < min_dist)
                {
                    min_dist = dist2line;
                    float dist2turn = DistanceFunc == null ? SampleDistFunc(route[i - 1], pointOnLine) : DistanceFunc(route[i - 1], pointOnLine);
                    distance_from_start = route_dist + dist2turn + dist2line;
                };
                route_dist += (DistanceFunc == null ? SampleDistFunc(route[i - 1], route[i]) : DistanceFunc(route[i - 1], route[i]));
            };
            return min_dist;
        }

        public struct InBoundInsert
        {
            public PointF[] poly;
            public int outboundIndex;
            public int inboundIndex;

            public InBoundInsert(PointF[] poly, int outboundIndex, int inboundIndex)
            {
                this.poly = poly;
                this.outboundIndex = outboundIndex;
                this.inboundIndex = inboundIndex;
            }
        }

        public class InBoundInsertComparer : IComparer<InBoundInsert>
        {
            public int Compare(InBoundInsert a, InBoundInsert b)
            {
                return a.outboundIndex.CompareTo(b.outboundIndex);
            }
        }
    }
}
