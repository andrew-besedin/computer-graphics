using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Лаб1WpfApp1
{
    public partial class Renderer
    {

        private void ClearBitmap(IntPtr bitmapDataPtr, int width, int height)
        {
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    unsafe
                    {
                        uint* rawPointer = (uint*)bitmapDataPtr;
                        var index = i * width + j;
                        rawPointer[index] = 0x0;
                    }
                }
            }
        }

        Matrix4x4 worldTransformation = new Matrix4x4
        (
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        );

        public float cameraSphereRadius = 100;
        public float cameraAngleX = 0;
        public float cameraAngleY = 0;

        float DegreesToRadians(float angle)
        {
            return (float)(angle / 180 * Math.PI);
        }

        private Vector4[] v4verticesBuffer = [];
        private int bufferLength;

        private void DrawLine(IntPtr bitmapDataPtr, int width, int height, int stride, int x1, int y1, int x2, int y2, uint color)
        {
            unsafe
            {
                byte* rawPointer = (byte*)bitmapDataPtr;

                int dx = Math.Abs(x2 - x1);
                int dy = Math.Abs(y2 - y1);
                int sx = x1 < x2 ? 1 : -1;
                int sy = y1 < y2 ? 1 : -1;
                int err = dx - dy;

                while (true)
                {
                    if (x1 >= 0 && x1 < width && y1 >= 0 && y1 < height)
                    {
                        var index = y1 * stride + x1 * 4;

                        rawPointer[index] = (byte)(color & 0xFF);
                        rawPointer[index + 1] = (byte)((color >> 8) & 0xFF);
                        rawPointer[index + 2] = (byte)((color >> 16) & 0xFF);
                        rawPointer[index + 3] = (byte)((color >> 24) & 0xFF);
                    }

                    if (x1 == x2 && y1 == y2) break;

                    int e2 = 2 * err;

                    if (e2 > -dy)
                    {
                        err -= dy;
                        x1 += sx;
                    }

                    if (e2 < dx)
                    {
                        err += dx;
                        y1 += sy;
                    }
                }
            }

        }

        private void ResizeBuffer(Obj obj)
        {
            int vertexCount = obj.vertices.Count;
            if (v4verticesBuffer.Length < vertexCount)
            {
                v4verticesBuffer = new Vector4[vertexCount];
            }
            bufferLength = vertexCount;
        }

        private Matrix4x4 GetCameraTransformation()
        {
            (float sinX, float cosX) = MathF.SinCos(DegreesToRadians(cameraAngleX));
            (float sinY, float cosY) = MathF.SinCos(DegreesToRadians(cameraAngleY));

            var eye = new Vector3();

            eye.X = (cosX * cosY * cameraSphereRadius);
            eye.Z = (sinX * cosY * cameraSphereRadius);
            eye.Y = (sinY * cameraSphereRadius);

            var target = new Vector3(0, 0, 0);

            var zAxis = Vector3.Normalize(eye - target);
            var xAxis = Vector3.Normalize(new Vector3(-sinX, 0, cosX));
            var yAxis = Vector3.Normalize(Vector3.Cross(xAxis, zAxis));


            Matrix4x4 view = new Matrix4x4(xAxis.X, yAxis.X, zAxis.X, 0,
                                          xAxis.Y, yAxis.Y, zAxis.Y, 0,
                                          xAxis.Z, yAxis.Z, zAxis.Z, 0,
                                          -Vector3.Dot(xAxis, eye),
                                          -Vector3.Dot(yAxis, eye),
                                          -Vector3.Dot(zAxis, eye),
                                          1);

            return view;
        }

        public void render(WriteableBitmap bitmap, Obj obj)
        {
            ResizeBuffer(obj);

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            // Init bitmap raw bits ptr
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


            Matrix4x4 modelToProjection = worldTransformation * cameraTransformation * projectionTransform;
            for (int i = 0; i < bufferLength; i++)
            {
                Vector4 v = new(obj.vertices[i], 1);
                v = Vector4.Transform(v, modelToProjection);
                v4verticesBuffer[i] = v;
            }

            List<Face> faces = obj.faces;
            int facesCount = faces.Count;
            uint color = 0xFFFFFFFF;

            for (int i = 0; i < facesCount; i++)
            {
                Face face = faces[i];
                int[] vIndices = face.vIndices;

                for (int j = 0; j < vIndices.Length; j++)
                {
                    int p0 = vIndices[j];
                    int p1 = vIndices[(j + 1) % vIndices.Length];
                    Vector4 v0 = v4verticesBuffer[p0];
                    Vector4 v1 = v4verticesBuffer[p1];

                    if (v0.X > v0.W && v1.X > v1.W)
                        continue;
                    if (v0.X < -v0.W && v1.X < -v1.W)
                        continue;
                    if (v0.Y > v0.W && v1.Y > v1.W)
                        continue;
                    if (v0.Y < -v0.W && v1.Y < -v1.W)
                        continue;
                    if (v0.Z > v0.W && v1.Z > v1.W)
                        continue;
                    if (v0.Z < 0 || v1.Z < 0)
                        continue;


                    v0 = Vector4.Transform(v0, viewPortTransform);
                    v0 *= (1 / v0.W);
                    v1 = Vector4.Transform(v1, viewPortTransform);
                    v1 *= (1 / v1.W);

                    DrawLine(bitmapDataPtr, width, height, stride, (int)v0.X, (int)v0.Y,
                       (int)v1.X, (int)v1.Y, color);
                }
            }

            bitmap.AddDirtyRect(new(0, 0, width, height));
            bitmap.Unlock();
        }
    }
}
