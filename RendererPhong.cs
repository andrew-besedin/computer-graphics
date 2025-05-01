using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Лаб1WpfApp1
{
    public struct Vertex
    {
        public Vector4 Position { get; set; }
        public Vector3 Normal { get; set; }
        public Vector3 CameraSpacePosition { get; set; }

        public static Vertex Lerp(Vertex a, Vertex b, float t)
        {
            return new Vertex
            {
                Position = Vector4.Lerp(a.Position, b.Position, t),
                Normal = Vector3.Lerp(a.Normal, b.Normal, t),
                CameraSpacePosition = Vector3.Lerp(a.CameraSpacePosition, b.CameraSpacePosition, t)
            };
        }
        public static Vertex operator +(Vertex lhs, Vertex rhs)
        {
            return new Vertex
            {
                Position = lhs.Position + rhs.Position,
                Normal = lhs.Normal + rhs.Normal,
                CameraSpacePosition = lhs.CameraSpacePosition + rhs.CameraSpacePosition
            };
        }
        public static Vertex operator -(Vertex lhs, Vertex rhs)
        {
            return new Vertex
            {
                Position = lhs.Position - rhs.Position,
                Normal = lhs.Normal - rhs.Normal,
                CameraSpacePosition = lhs.CameraSpacePosition - rhs.CameraSpacePosition
            };
        }
        public static Vertex operator *(Vertex lhs, float scalar)
        {
            return new Vertex
            {
                Position = lhs.Position * scalar,
                Normal = lhs.Normal * scalar,
                CameraSpacePosition = lhs.CameraSpacePosition * scalar
            };
        }
        public static Vertex operator *(float scalar, Vertex rhs)
        {
            return new Vertex
            {
                Position = rhs.Position * scalar,
                Normal = rhs.Normal * scalar,
                CameraSpacePosition = rhs.CameraSpacePosition * scalar
            };
        }
        public static Vertex operator /(Vertex lhs, float scalar)
        {
            return new Vertex
            {
                Position = lhs.Position / scalar,
                Normal = lhs.Normal / scalar,
                CameraSpacePosition = lhs.CameraSpacePosition / scalar
            };
        }
    }

    partial class Renderer
    {
        int screenWidth = 0;
        int screenHeight = 0;
        IntPtr bitmapDataPtr;

        private void DrawTrianglePhong(in Vertex p0, in Vertex p1, in Vertex p2)
        {
            Vertex min = p0;
            Vertex mid = p1;
            Vertex max = p2;
            // Correct min, mid and max
            if (mid.Position.Y < min.Position.Y)
            {
                (min, mid) = (mid, min);
            }
            if (max.Position.Y < min.Position.Y)
            {
                (min, max) = (max, min);
            }
            if (max.Position.Y < mid.Position.Y)
            {
                (mid, max) = (max, mid);
            }

            if (min.Position.Y == mid.Position.Y)
            {
                //flat top
                if (mid.Position.X < min.Position.X)
                {
                    (min, mid) = (mid, min);
                }
                DrawFlatTopTriangle(min, mid, max);
            }
            else if (max.Position.Y == mid.Position.Y)
            {
                //flat bottom
                if (max.Position.X > mid.Position.X)
                {
                    (mid, max) = (max, mid);
                }
                DrawFlatBottomTriangle(min, mid, max);
            }
            else
            {
                float c = (mid.Position.Y - min.Position.Y) / (max.Position.Y - min.Position.Y);
                Vertex interpolant = Vertex.Lerp(min, max, c);
                if (interpolant.Position.X > mid.Position.X)
                {
                    //right major
                    DrawFlatBottomTriangle(min, interpolant, mid);
                    DrawFlatTopTriangle(mid, interpolant, max);
                }
                else
                {
                    //left major
                    DrawFlatBottomTriangle(min, mid, interpolant);
                    DrawFlatTopTriangle(interpolant, mid, max);
                }
            }
        }
        void DrawFlatTopTriangle(in Vertex leftTopPoint, in Vertex rightTopPoint, in Vertex bottomPoint)
        {
            float dy = bottomPoint.Position.Y - leftTopPoint.Position.Y;
            Vertex dLeftPoint = (bottomPoint - leftTopPoint) / dy;
            Vertex dRightPoint = (bottomPoint - rightTopPoint) / dy;
            Vertex dLineInterpolant = (rightTopPoint - leftTopPoint) / (rightTopPoint.Position.X - leftTopPoint.Position.X);
            Vertex rightPoint = rightTopPoint;
            DrawFlatTriangle(leftTopPoint, rightPoint, bottomPoint.Position.Y, dLeftPoint, dRightPoint, dLineInterpolant);
        }
        void DrawFlatBottomTriangle(in Vertex topPoint, in Vertex rightBottomPoint, in Vertex leftBottomPoint)
        {
            float dy = rightBottomPoint.Position.Y - topPoint.Position.Y;
            Vertex dRightPoint = (rightBottomPoint - topPoint) / dy;
            Vertex dLeftPoint = (leftBottomPoint - topPoint) / dy;
            Vertex rightPoint = topPoint;
            Vertex DLineInterpolant = (rightBottomPoint - leftBottomPoint) / (rightBottomPoint.Position.X - leftBottomPoint.Position.X);
            DrawFlatTriangle(topPoint, rightPoint, rightBottomPoint.Position.Y, dLeftPoint, dRightPoint, DLineInterpolant);
        }
        void DrawFlatTriangle(Vertex leftPoint, Vertex rightPoint, float yMax, in Vertex dLeftPoint, in Vertex dRightPoint, in Vertex dLineInterpolant)
        {
            int yStart = Math.Max((int)MathF.Ceiling(leftPoint.Position.Y), 0);
            int yEnd = Math.Min((int)MathF.Ceiling(yMax), screenHeight);
            float yPrestep = yStart - leftPoint.Position.Y;
            leftPoint += dLeftPoint * yPrestep;
            rightPoint += dRightPoint * yPrestep;
            for (int y = yStart; y < yEnd; y++, leftPoint += dLeftPoint, rightPoint += dRightPoint)
            {
                int xStart = Math.Max((int)MathF.Ceiling(leftPoint.Position.X), 0);
                int xEnd = Math.Min((int)MathF.Ceiling(rightPoint.Position.X), screenWidth);
                if (xStart >= xEnd)
                {
                    continue;
                }
                float xPrestep = xStart - leftPoint.Position.X;
                Vertex lineInterpolant = leftPoint + xPrestep * dLineInterpolant;
                for (int x = xStart; x < xEnd; x++, lineInterpolant += dLineInterpolant)
                {
                    float z = lineInterpolant.Position.Z;

                    if (zbuffer.TestAndSet(x, y, z))
                    {
                        Vertex correctedPoint = lineInterpolant * (1 / lineInterpolant.Position.W);

                        unsafe
                        {
                            uint* rawPointer = (uint*)bitmapDataPtr;
                            var index = y * screenWidth + x;
                            rawPointer[index] = this.PixelShader(correctedPoint);
                        }
                    }
                }
            }
        }

        public uint PixelShader(Vertex input)
        {
            float lightIntensity = 0.8f;
            float ambientIntensity = 0.2f;
            var lightColor = new Vector3(1);
            var ambientColor = new Vector3(1);
            var diffuseColor = new Vector3(0, 0, 1);
            float specularPower = 50f;

            Vector3 ambient = ambientColor * ambientIntensity * diffuseColor;

            Vector3 lightDir = -Vector3.Normalize(new(-1, -1, -1));


            Vector3 camDir = -Vector3.Normalize(input.CameraSpacePosition);
            Vector3 normal = Vector3.Normalize(input.Normal);

            Vector3 reflectDir = Vector3.Reflect(-lightDir, normal);
            float diffuseFactor = Math.Max(Vector3.Dot(normal, lightDir), 0);
            Vector3 diffuse = diffuseColor * diffuseFactor * lightIntensity;
            float specularFactor = MathF.Pow(Math.Max(Vector3.Dot(reflectDir, camDir), 0), specularPower);
            Vector3 specular = lightColor * specularFactor * lightIntensity;
            Vector3 finalColor = Vector3.Clamp(ambient + diffuse + specular, Vector3.Zero, new Vector3(1, 1, 1));
            uint color = (uint)0xFF << 24
                         | (uint)(finalColor.X * 0xFF) << 16
                         | (uint)(finalColor.Y * 0xFF) << 8
                         | (uint)(finalColor.Z * 0xFF);
            return color;
        }



        public Vertex GetVertexWithWorldPositionFromFace(Obj obj, int faceIndex, int vertexIndex)
        {
            var cameraTransformation = this.GetCameraTransformation();

            Face face = obj.faces[faceIndex];
            Vertex vertex = default;
            vertex.Position = Vector4.Transform(new Vector4(obj.vertices[face.vIndices[vertexIndex]], 1), worldTransformation);
            if (face.nIndices == null)
                throw new ArgumentException("Face has no normal indices.");

            Matrix4x4.Invert(worldTransformation, out var invertedWorldTransform);
            var worldNormalTransform = Matrix4x4.Transpose(invertedWorldTransform);

            vertex.Normal = Vector3.TransformNormal(Vector3.TransformNormal(obj.normals[face.nIndices[vertexIndex]], worldNormalTransform), cameraTransformation);
            vertex.CameraSpacePosition = Vector3.Transform(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), cameraTransformation);

            return vertex;
        }

        public void RenderPhong(WriteableBitmap bitmap, Obj obj)
        {
            ResizeBuffer(obj);

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;

            screenWidth = width;
            screenHeight = height;

            ResizeAndClearZBuffer(width, height);

            bitmap.Lock();

            IntPtr bitmapDataPtr = bitmap.BackBuffer;
            this.bitmapDataPtr = bitmapDataPtr;

            ClearBitmap(bitmapDataPtr, width, height);

            int stride = bitmap.BackBufferStride;


            var cameraTransformation = this.GetCameraTransformation();

            float aspectRatio = (float)width / height;
            float fovVertical = MathF.PI / 3 / aspectRatio;
            float nearPlaneDistance = 0.01f;
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

            List<Face> faces = obj.faces;
            int facesCount = faces.Count;

            for (int i = 0; i < facesCount; i++)
            {
                Face face = faces[i];
                int[] vIndices = face.vIndices;

                for (int j = 0; j < vIndices.Length - 2; j++)
                {
                    Vertex vertex0 = this.GetVertexWithWorldPositionFromFace(obj, i, 0);
                    Vertex vertex1 = this.GetVertexWithWorldPositionFromFace(obj, i, j + 1);
                    Vertex vertex2 = this.GetVertexWithWorldPositionFromFace(obj, i, j + 2);

                    vertex0.Position = Vector4.Transform(vertex0.Position, cameraTransformation);
                    vertex1.Position = Vector4.Transform(vertex1.Position, cameraTransformation);
                    vertex2.Position = Vector4.Transform(vertex2.Position, cameraTransformation);


                    var vector1 = (vertex2.Position - vertex0.Position);
                    var vector2 = (vertex1.Position - vertex0.Position);

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

                    var isInvisibleTriangle = Vector3.Dot(normalTriangleVector, new Vector3(vertex0.Position.X, vertex0.Position.Y, vertex0.Position.Z)) < 0;

                    vertex0.Position = Vector4.Transform(vertex0.Position, projectionTransform);
                    vertex1.Position = Vector4.Transform(vertex1.Position, projectionTransform);
                    vertex2.Position = Vector4.Transform(vertex2.Position, projectionTransform);


                    if (isInvisibleTriangle) continue;
                    if (vertex0.Position.X > vertex0.Position.W && vertex1.Position.X > vertex1.Position.W && vertex2.Position.X > vertex2.Position.W)
                        continue;
                    if (vertex0.Position.X < -vertex0.Position.W && vertex1.Position.X < -vertex1.Position.W && vertex2.Position.X < -vertex2.Position.W)
                        continue;
                    if (vertex0.Position.Y > vertex0.Position.W && vertex1.Position.Y > vertex1.Position.W && vertex2.Position.Y > vertex2.Position.W)
                        continue;
                    if (vertex0.Position.Y < -vertex0.Position.W && vertex1.Position.Y < -vertex1.Position.W && vertex2.Position.Y < -vertex2.Position.W)
                        continue;
                    if (vertex0.Position.Z > vertex0.Position.W && vertex1.Position.Z > vertex1.Position.W && vertex2.Position.Z > vertex2.Position.W)
                        continue;
                    if (vertex0.Position.Z < 0 || vertex1.Position.Z < 0 || vertex2.Position.Z < 0)
                        continue;

                    void TransformToViewPort(ref Vertex vertex)
                    {
                        float invZ = 1 / vertex.Position.W;

                        vertex *= invZ;
                        Vector4 ndcPosition = Vector4.Transform(vertex.Position, viewPortTransform);

                        ndcPosition.W = invZ;
                        vertex.Position = ndcPosition;
                    }


                    TransformToViewPort(ref vertex0);
                    TransformToViewPort(ref vertex1);
                    TransformToViewPort(ref vertex2);

                    DrawTrianglePhong(vertex0, vertex1, vertex2);
                }
            }

            bitmap.AddDirtyRect(new(0, 0, width, height));
            bitmap.Unlock();
        }
    }
}
