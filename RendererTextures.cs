using GraphicsLib.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Лаб1WpfApp1
{
    partial class Renderer
    {
        CubeMappingTextures? cubeMappingTextures;
        Material currentMaterial;

        private Vector4 ToCameraSpace(Vector4 vector)
        {
            var cameraTransformation = this.GetCameraTransformation();
            return Vector4.Transform(vector, worldTransformation * cameraTransformation);
        }

        private Vector3 ToCameraSpace(Vector3 vector)
        {
            var cameraTransformation = this.GetCameraTransformation();
            return Vector3.TransformNormal(Vector3.TransformNormal(vector, worldTransformation), cameraTransformation);
        }

        private static Vector3 ToVector3(Vector4 vector)
        {
            return new Vector3(vector.X, vector.Y, vector.Z);
        }

        private void DrawTriangle_Textures(in Vertex p0, in Vertex p1, in Vertex p2)
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
                DrawFlatTopTriangle_Textures(min, mid, max);
            }
            else if (max.Position.Y == mid.Position.Y)
            {
                //flat bottom
                if (max.Position.X > mid.Position.X)
                {
                    (mid, max) = (max, mid);
                }
                DrawFlatBottomTriangle_Textures(min, mid, max);
            }
            else
            {
                float c = (mid.Position.Y - min.Position.Y) / (max.Position.Y - min.Position.Y);
                Vertex interpolant = Vertex.Lerp(min, max, c);
                if (interpolant.Position.X > mid.Position.X)
                {
                    //right major
                    DrawFlatBottomTriangle_Textures(min, interpolant, mid);
                    DrawFlatTopTriangle_Textures(mid, interpolant, max);
                }
                else
                {
                    //left major
                    DrawFlatBottomTriangle_Textures(min, mid, interpolant);
                    DrawFlatTopTriangle_Textures(interpolant, mid, max);
                }
            }
        }
        void DrawFlatTopTriangle_Textures(in Vertex leftTopPoint, in Vertex rightTopPoint, in Vertex bottomPoint)
        {
            float dy = bottomPoint.Position.Y - leftTopPoint.Position.Y;
            Vertex dLeftPoint = (bottomPoint - leftTopPoint) / dy;
            Vertex dRightPoint = (bottomPoint - rightTopPoint) / dy;
            Vertex dLineInterpolant = (rightTopPoint - leftTopPoint) / (rightTopPoint.Position.X - leftTopPoint.Position.X);
            Vertex rightPoint = rightTopPoint;
            DrawFlatTriangle_Textures(leftTopPoint, rightPoint, bottomPoint.Position.Y, dLeftPoint, dRightPoint, dLineInterpolant);
        }
        void DrawFlatBottomTriangle_Textures(in Vertex topPoint, in Vertex rightBottomPoint, in Vertex leftBottomPoint)
        {
            float dy = rightBottomPoint.Position.Y - topPoint.Position.Y;
            Vertex dRightPoint = (rightBottomPoint - topPoint) / dy;
            Vertex dLeftPoint = (leftBottomPoint - topPoint) / dy;
            Vertex rightPoint = topPoint;
            Vertex DLineInterpolant = (rightBottomPoint - leftBottomPoint) / (rightBottomPoint.Position.X - leftBottomPoint.Position.X);
            DrawFlatTriangle_Textures(topPoint, rightPoint, rightBottomPoint.Position.Y, dLeftPoint, dRightPoint, DLineInterpolant);
        }
        void DrawFlatTriangle_Textures(Vertex leftPoint, Vertex rightPoint, float yMax, in Vertex dLeftPoint, in Vertex dRightPoint, in Vertex dLineInterpolant)
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
                    float z = -lineInterpolant.Position.W;

                    if (zbuffer.TestAndSet(x, y, z))
                    {
                        Vertex correctedPoint = lineInterpolant * (1 / lineInterpolant.Position.W);

                        unsafe
                        {
                            uint* rawPointer = (uint*)bitmapDataPtr;
                            var index = y * screenWidth + x;
                            rawPointer[index] = this.PixelShader_Textures(correctedPoint);
                        }
                    }
                }
            }
        }

        public uint PixelShader_Textures(Vertex input)
        {
            //input.Uv = new Vector2(input.Uv.Y, input.Uv.X);

            if (currentMaterial == null)
            {
                throw new InvalidOperationException("currentMaterial is not defined.");
            }

            float ambientIntensity = 0.2f;
            var lightColor = new Vector3(1);
            var ambientColor = new Vector3(1);
            float lightIntensity = 0.8f;



            var diffuseColor = new Vector3(1, 1, 1);

            if (currentMaterial.Map_Kd != null)
            {
                diffuseColor = ToVector3(currentMaterial.Map_Kd.Value.SampleNearest(input.Uv));
            } else if (currentMaterial.Kd != null)
            {
                diffuseColor = new Vector3(currentMaterial.Kd);
            }

            Vector3 normal = Vector3.Normalize(input.Normal);

            if (currentMaterial.Map_Bump != null)
            {
                normal = Vector3.Normalize(
                    ToCameraSpace(
                        ToVector3(
                            currentMaterial.Map_Bump.Value.SampleNearest(input.Uv)) * 2 - Vector3.One
                           )
                    );
            }

            Vector3 specularCoef = new(0.8f, 0.8f, 0.8f);

            if (currentMaterial.Map_Ks != null)
            {
                specularCoef = ToVector3(currentMaterial.Map_Ks.Value.SampleNearest(input.Uv));
            } else if (currentMaterial.Ks != null)
            {
                specularCoef = new Vector3(currentMaterial.Ks);
            }

            float specularPower = currentMaterial.Ni;


            Vector3 ambient = ambientColor * ambientIntensity * diffuseColor;

            Vector3 lightDir = Vector3.Normalize(new(1, 1, 1));


            Vector3 camDir = Vector3.Normalize(input.CameraSpacePosition);

            Vector3 reflectDir = Vector3.Reflect(lightDir, normal);

            Matrix4x4.Invert(this.GetCameraTransformation(), out Matrix4x4 invCameraTransform);

            Vector3 cameraReflectDir = Vector3.TransformNormal(Vector3.Reflect(camDir, normal), invCameraTransform);

            var reflectColor = cubeMappingTextures != null
                ? ToVector3(cubeMappingTextures.SampleBackground(cameraReflectDir))
                : lightColor;

            float diffuseFactor = Math.Max(Vector3.Dot(normal, lightDir), 0);
            Vector3 diffuse = diffuseColor * diffuseFactor * lightIntensity;
            float specularFactor = MathF.Pow(Math.Max(Vector3.Dot(reflectDir, camDir), 0), specularPower);
            Vector3 specular = reflectColor * specularFactor * specularCoef;
            Vector3 finalColor = Vector3.Clamp(ambient + diffuse + specular, Vector3.Zero, new Vector3(1, 1, 1));
            uint color = (uint)0xFF << 24
                         | (uint)(finalColor.X * 0xFF) << 16
                         | (uint)(finalColor.Y * 0xFF) << 8
                         | (uint)(finalColor.Z * 0xFF);
            return color;
        }



        public Vertex GetVertexWithWorldPositionFromFace_Textures(Obj obj, int faceIndex, int vertexIndex)
        {
            var cameraTransformation = this.GetCameraTransformation();

            Face face = obj.faces[faceIndex];
            Vertex vertex = default;
            vertex.Position = Vector4.Transform(new Vector4(obj.vertices[face.vIndices[vertexIndex]], 1), worldTransformation);
            if (face.nIndices == null)
                throw new ArgumentException("Face has no normal indices.");
            if (face.tIndices == null)
                throw new ArgumentException("Face has no texture indices.");

            Matrix4x4.Invert(worldTransformation, out var invertedWorldTransform);
            var worldNormalTransform = Matrix4x4.Transpose(invertedWorldTransform);

            vertex.Normal = Vector3.TransformNormal(Vector3.TransformNormal(obj.normals[face.nIndices[vertexIndex]], worldNormalTransform), cameraTransformation);
            vertex.CameraSpacePosition = Vector3.Transform(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), cameraTransformation);
            vertex.Uv = obj.uvs[face.tIndices[vertexIndex]];

            return vertex;
        }

        private void FillBackground()
        {
            if (cubeMappingTextures == null)
            {
                return;
            }

            for (int y = 0; y < screenHeight; y++)
            {
                for (int x = 0; x < screenWidth; x++)
                {
                    Matrix4x4.Invert(this.GetProjectionTransform(), out Matrix4x4 invProjectionTransform);
                    Matrix4x4.Invert(this.GetCameraTransformation(), out Matrix4x4 invCameraTransform);


                    Vector3 rayOrigin = this.GetCameraWorldPos();
                    Vector3 farNdc = new Vector3(((float)x / screenWidth) * 2.0f - 1.0f,
                        1.0f - ((float)y / screenHeight) * 2.0f, 0.999f);
                    Vector4 farView = Vector4.Transform(new Vector4(farNdc, 1), invProjectionTransform);
                    farView /= farView.W;
                    farView = Vector4.Transform(farView, invCameraTransform);
                    Vector3 rayDirection = Vector3.Normalize(ToVector3(farView) - rayOrigin);

                    var pixelColor = ToVector3(cubeMappingTextures.SampleBackground(rayDirection));

                    uint color = (uint)(0xFF) << 24
                        | (uint)(pixelColor.X * 0xFF) << 16
                        | (uint)(pixelColor.Y * 0xFF) << 8
                        | (uint)(pixelColor.Z * 0xFF);

                    unsafe
                    {
                        uint* rawPointer = (uint*)bitmapDataPtr;
                        var index = y * screenWidth + x;
                        rawPointer[index] = color;
                    }
                }
            }
        }

        private Matrix4x4 GetProjectionTransform()
        {
            float aspectRatio = (float)screenWidth / screenHeight;
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

            return projectionTransform;
        }

        public void RenderTextures(WriteableBitmap bitmap, Obj obj, CubeMappingTextures? cubeMappingTextures)
        {
            this.cubeMappingTextures = cubeMappingTextures;

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

            if (cubeMappingTextures != null)
            {
                this.FillBackground();
            }


            var cameraTransformation = this.GetCameraTransformation();

            var projectionTransform = this.GetProjectionTransform();

            float leftCornerX = 0;
            float leftCornerY = 0;
            Matrix4x4 viewPortTransform = new Matrix4x4(
                (float)width / 2, 0, 0, 0,
                0, -(float)height / 2, 0, 0,
                0, 0, 1, 0,
                leftCornerX + (float)width / 2, leftCornerY + (float)height / 2, 0, 1);

            List<Face> faces = obj.faces;
            int facesCount = faces.Count;

            for (int i = 0; i < facesCount; i++)
            {
                Face face = faces[i];
                int[] vIndices = face.vIndices;

                currentMaterial = face.material;

                for (int j = 0; j < vIndices.Length - 2; j++)
                {
                    Vertex vertex0 = this.GetVertexWithWorldPositionFromFace_Textures(obj, i, 0);
                    Vertex vertex1 = this.GetVertexWithWorldPositionFromFace_Textures(obj, i, j + 1);
                    Vertex vertex2 = this.GetVertexWithWorldPositionFromFace_Textures(obj, i, j + 2);

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

                    DrawTriangle_Textures(vertex0, vertex1, vertex2);
                }
            }

            bitmap.AddDirtyRect(new(0, 0, width, height));
            bitmap.Unlock();
        }
    }
}
