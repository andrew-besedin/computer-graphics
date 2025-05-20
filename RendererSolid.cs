using GraphicsLib.Types;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Windows.Media.Imaging;

namespace Лаб1WpfApp1
{
    public partial class Renderer
    {
        private Zbuffer zbuffer = new(100, 100);

        private void ResizeAndClearZBuffer(int width, int height)
        {
            if (zbuffer.Width != width && zbuffer.Height != height)
            {
                zbuffer = new Zbuffer(width, height);
            }
            else
            {
                zbuffer.Clear();
            }
        }

        public void DrawTriangle(IntPtr bitmapDataPtr, int width, int height, int stride, Vector4 v0, Vector4 v1, Vector4 v2, uint color, Zbuffer zbuffer)
        {
            Vector4 min = v0;
            Vector4 mid = v1;
            Vector4 max = v2;

            if (mid.Y < min.Y)
            {
                (min, mid) = (mid, min);
            }
            if (max.Y < min.Y)
            {
                (min, max) = (max, min);
            }
            if (max.Y < mid.Y)
            {
                (mid, max) = (max, mid);
            }

            if (min.Y == mid.Y)
            {
                if (mid.X < min.X)
                {
                    (min, mid) = (mid, min);
                }
                DrawFlatTopTriangleWithZBuffer(bitmapDataPtr, width, height, min, mid, max, color, zbuffer);
            }
            else if (max.Y == mid.Y)
            {
                if (max.X < mid.X)
                {
                    (mid, max) = (max, mid);
                }
                DrawFlatBottomTriangleWithZBuffer(bitmapDataPtr, width, height, min, mid, max, color, zbuffer);
            }
            else
            {
                float c = (mid.Y - min.Y) / (max.Y - min.Y);
                Vector4 interpolant = Vector4.Lerp(min, max, c);
                if (interpolant.X > mid.X)
                {
                    DrawFlatBottomTriangleWithZBuffer(bitmapDataPtr, width, height, min, interpolant, mid, color, zbuffer);
                    DrawFlatTopTriangleWithZBuffer(bitmapDataPtr, width, height, mid, interpolant, max, color, zbuffer);
                }
                else
                {                    
                    DrawFlatBottomTriangleWithZBuffer(bitmapDataPtr, width, height, min, mid, interpolant, color, zbuffer);
                    DrawFlatTopTriangleWithZBuffer(bitmapDataPtr, width, height, interpolant, mid, max, color, zbuffer);
                }
            }
        }

        private static void DrawFlatTopTriangleWithZBuffer(IntPtr bitmapDataPtr, int bitmapWidth, int bitmapHeight,
            Vector4 leftTopPoint, Vector4 rightTopPoint, Vector4 bottomPoint, uint color, Zbuffer zbuffer)
        {
            float dy = bottomPoint.Y - leftTopPoint.Y;
            Vector4 dLeftPoint = (bottomPoint - leftTopPoint) / dy;
            Vector4 dRightPoint = (bottomPoint - rightTopPoint) / dy;
            Vector4 rightPoint = rightTopPoint;
            DrawFlatTriangleWithZBuffer(bitmapDataPtr, bitmapWidth, bitmapHeight, leftTopPoint, rightPoint, bottomPoint,
                                                dLeftPoint, dRightPoint, color, zbuffer);
        }
        private static void DrawFlatBottomTriangleWithZBuffer(IntPtr bitmapDataPtr, int bitmapWidth, int bitmapHeight,
            Vector4 topPoint, Vector4 rightBottomPoint, Vector4 leftBottomPoint, uint color, Zbuffer zbuffer)
        {
            float dy = rightBottomPoint.Y - topPoint.Y;
            Vector4 dRightPoint = (rightBottomPoint - topPoint) / dy;
            Vector4 dLeftPoint = (leftBottomPoint - topPoint) / dy;
            Vector4 rightPoint = topPoint;
            DrawFlatTriangleWithZBuffer(bitmapDataPtr, bitmapWidth, bitmapHeight, topPoint, rightPoint, rightBottomPoint, dLeftPoint, dRightPoint, color, zbuffer);
        }
        private static void DrawFlatTriangleWithZBuffer(IntPtr bitmapDataPtr, int bitmapWidth, int bitmapHeight,
            Vector4 leftPoint, Vector4 rightPoint, Vector4 EndPoint, Vector4 dLeftPoint, Vector4 dRightPoint, uint color, Zbuffer zbuffer)
        {
            int yStart = Math.Max((int)Math.Ceiling(leftPoint.Y), 0);
            int yEnd = Math.Min((int)Math.Ceiling(EndPoint.Y), bitmapHeight - 1);
            float yTop = leftPoint.Y;
            leftPoint += dLeftPoint * (yStart - yTop);
            rightPoint += dRightPoint * (yStart - yTop);
            for (int y = yStart; y < yEnd; y++)
            {
                int xStart = Math.Max((int)Math.Ceiling(leftPoint.X), 0);
                int xEnd = Math.Min((int)Math.Ceiling(rightPoint.X), bitmapWidth - 1);
                Vector4 lineInterpolant = leftPoint;
                float dx = rightPoint.X - leftPoint.X;
                Vector4 dLine = (rightPoint - leftPoint) / dx;
                lineInterpolant += dLine * (xStart - leftPoint.X);
                for (int x = xStart; x < xEnd; x++)
                {
                    float z = lineInterpolant.Z;
                    if (zbuffer.TestAndSet(x, y, z))
                    {
                        unsafe
                        {
                            uint* rawPointer = (uint*)bitmapDataPtr;
                            var index = y * bitmapWidth + x;
                            rawPointer[index] = color;
                        }
                    }
                    lineInterpolant += dLine;
                }
                leftPoint += dLeftPoint;
                rightPoint += dRightPoint;
            }

        }


        public void RenderSolid(WriteableBitmap bitmap, Obj obj)
        {
            ResizeBuffer(obj);

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            ResizeAndClearZBuffer(width, height);

            bitmap.Lock();

            IntPtr bitmapDataPtr = bitmap.BackBuffer;

            ClearBitmap(bitmapDataPtr, width, height);

            int stride = bitmap.BackBufferStride;


            var cameraTransformation = this.GetCameraTransformation();

            float aspectRatio = (float)width / height;
            float fovVertical = MathF.PI / 3 / aspectRatio;
            float nearPlaneDistance = 1f;
            float farPlaneDistance = float.PositiveInfinity;
            float zCoeff = (float.IsPositiveInfinity(farPlaneDistance) ? -1f : farPlaneDistance / (nearPlaneDistance - farPlaneDistance));

            Matrix4x4 projectionTransform = new Matrix4x4(
                1 / MathF.Tan(fovVertical * 0.5f) / aspectRatio, 0, 0, 0,
                0, 1 / MathF.Tan(fovVertical * 0.5f), 0, 0,
                0, 0, zCoeff, -1,
                0, 0, zCoeff * nearPlaneDistance, 0
            );

            float leftCornerX = 0;
            float leftCornerY = 0;
            Matrix4x4 viewPortTransform = new Matrix4x4(
                (float)width / 2, 0, 0, 0,
                0, -(float)height / 2, 0, 0,
                0, 0, 1, 0,
                leftCornerX + (float)width / 2, leftCornerY + (float)height / 2, 0, 1);


            Matrix4x4 modelToProjection = worldTransformation * cameraTransformation;
            for (int i = 0; i < bufferLength; i++)
            {
                Vector4 v = new(obj.vertices[i], 1);
                v = Vector4.Transform(v, modelToProjection);
                v4verticesBuffer[i] = v;
            }

            List<Face> faces = obj.faces;
            int facesCount = faces.Count;

            for (int i = 0; i < facesCount; i++)
            {
                Face face = faces[i];
                int[] vIndices = face.vIndices;

                for (int j = 0; j < vIndices.Length - 2; j++)
                {
                    int p0 = vIndices[0];
                    int p1 = vIndices[j + 1];
                    int p2 = vIndices[j + 2];

                    Vector4 v0 = v4verticesBuffer[p0];
                    Vector4 v1 = v4verticesBuffer[p1];
                    Vector4 v2 = v4verticesBuffer[p2];

                    Vector3 lightDirection = Vector3.Normalize(new(-1, -1, -1));


                    var vector1 = (v2 - v0);
                    var vector2 = (v1 - v0);

                    var normalTriangleVector = Vector3.Normalize(Vector3.Cross(
                        new Vector3(
                            vector2.X,
                            vector2.Y,
                            vector2.Z
                        ),
                        new Vector3(
                            vector1.X,
                            vector1.Y,
                            vector1.Z
                        )
                    ));

                    var illuminationValue = float.Clamp(Vector3.Dot(normalTriangleVector, lightDirection), 0, 1);
                    var isInvisibleTriangle = Vector3.Dot(normalTriangleVector, new Vector3(v0.X, v0.Y, v0.Z)) < 0;

                    v0 = Vector4.Transform(v0, projectionTransform);
                    v1 = Vector4.Transform(v1, projectionTransform);
                    v2 = Vector4.Transform(v2, projectionTransform);


                    if (isInvisibleTriangle) continue;
                    if (v0.X > v0.W && v1.X > v1.W && v2.X > v2.W)
                        continue;
                    if (v0.X < -v0.W && v1.X < -v1.W && v2.X < -v2.W)
                        continue;
                    if (v0.Y > v0.W && v1.Y > v1.W && v2.Y > v2.W)
                        continue;
                    if (v0.Y < -v0.W && v1.Y < -v1.W && v2.Y < -v2.W)
                        continue;
                    if (v0.Z > v0.W && v1.Z > v1.W && v2.Z > v2.W)
                        continue;
                    if (v0.Z < 0 || v1.Z < 0 || v2.Z < 0)
                        continue;


                    v0 = Vector4.Transform(v0, viewPortTransform);
                    v0 *= (1 / v0.W);
                    v1 = Vector4.Transform(v1, viewPortTransform);
                    v1 *= (1 / v1.W);
                    v2 = Vector4.Transform(v2, viewPortTransform);
                    v2 *= (1 / v2.W);

                    byte rgb = (byte)(illuminationValue * 0xFF);

                    uint color = (uint)(0xFF000000 | (rgb) | (rgb << 8) | (rgb << 16));

                    DrawTriangle(bitmapDataPtr, width, height, stride, v0, v1, v2, color, zbuffer);
                }
            }

            bitmap.AddDirtyRect(new(0, 0, width, height));
            bitmap.Unlock();
        }
    }
}